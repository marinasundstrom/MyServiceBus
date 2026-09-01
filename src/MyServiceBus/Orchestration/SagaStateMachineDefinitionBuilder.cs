namespace MyServiceBus.Orchestration;

/// <summary>
/// Builds a normalized saga state-machine declaration.
/// </summary>
public sealed class SagaStateMachineDefinitionBuilder
{
    private readonly string stateMachineId;
    private readonly string definitionVersion;
    private readonly string owner;
    private readonly string sagaDataUrn;
    private readonly string stateMember;
    private readonly List<SagaStateDefinition> states = new();
    private readonly List<SagaEventDefinition> events = new();
    private readonly List<SagaBehaviorDefinition> behaviors = new();
    private SagaCompletionPolicy completionPolicy = SagaCompletionPolicy.Retain;
    private SagaRepositoryRequirements repositoryRequirements = new(
        SagaCorrelationKind.Identity,
        SagaConcurrencyKind.SingleProcess,
        SagaDurabilityKind.Volatile,
        SagaOutboxKind.Logical);

    public SagaStateMachineDefinitionBuilder(
        string stateMachineId,
        string definitionVersion,
        string owner,
        string sagaDataUrn,
        string stateMember)
    {
        this.stateMachineId = Required(stateMachineId, nameof(stateMachineId));
        this.definitionVersion = Required(definitionVersion, nameof(definitionVersion));
        this.owner = Required(owner, nameof(owner));
        this.sagaDataUrn = Required(sagaDataUrn, nameof(sagaDataUrn));
        this.stateMember = Required(stateMember, nameof(stateMember));
    }

    public SagaStateMachineDefinitionBuilder State(string id)
    {
        var stateId = Required(id, nameof(id));
        if (states.Any(state => state.Id == stateId))
            throw new ArgumentException($"Saga state '{stateId}' is already declared.", nameof(id));
        states.Add(new SagaStateDefinition(stateId));
        return this;
    }

    public SagaStateMachineDefinitionBuilder Event<TMessage>(
        string id,
        Action<SagaEventDefinitionBuilder> configure)
        => Event(id, MessageUrn.For(typeof(TMessage)), configure);

    public SagaStateMachineDefinitionBuilder Event(
        string id,
        string messageUrn,
        Action<SagaEventDefinitionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var eventId = Required(id, nameof(id));
        if (events.Any(@event => @event.Id == eventId))
            throw new ArgumentException($"Saga event '{eventId}' is already declared.", nameof(id));

        var builder = new SagaEventDefinitionBuilder(eventId, Required(messageUrn, nameof(messageUrn)));
        configure(builder);
        events.Add(builder.Build());
        return this;
    }

    public SagaStateMachineDefinitionBuilder Initially(
        string eventId,
        Action<SagaBehaviorDefinitionBuilder> configure)
        => Behavior(SagaStateMachineDefinition.InitialState, eventId, configure);

    public SagaStateMachineDefinitionBuilder During(
        string state,
        string eventId,
        Action<SagaBehaviorDefinitionBuilder> configure)
        => Behavior(Required(state, nameof(state)), eventId, configure);

    public SagaStateMachineDefinitionBuilder DuringAny(
        string eventId,
        Action<SagaBehaviorDefinitionBuilder> configure)
        => Behavior(SagaStateMachineDefinition.AnyState, eventId, configure);

    public SagaStateMachineDefinitionBuilder DeleteWhenFinalized()
    {
        completionPolicy = SagaCompletionPolicy.DeleteWhenFinalized;
        return this;
    }

    public SagaStateMachineDefinitionBuilder RetainWhenFinalized()
    {
        completionPolicy = SagaCompletionPolicy.Retain;
        return this;
    }

    public SagaStateMachineDefinitionBuilder Requires(SagaRepositoryRequirements requirements)
    {
        repositoryRequirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        return this;
    }

    public SagaStateMachineDefinition Build()
    {
        var definition = new SagaStateMachineDefinition(
            SagaStateMachineDefinition.CurrentSchemaVersion,
            stateMachineId,
            definitionVersion,
            owner,
            sagaDataUrn,
            stateMember,
            completionPolicy,
            repositoryRequirements,
            states.OrderBy(state => state.Id, StringComparer.Ordinal).ToArray(),
            events.OrderBy(@event => @event.Id, StringComparer.Ordinal).ToArray(),
            behaviors
                .OrderBy(behavior => behavior.SourceState, StringComparer.Ordinal)
                .ThenBy(behavior => behavior.EventId, StringComparer.Ordinal)
                .ToArray());
        definition.Validate();
        return definition;
    }

    private SagaStateMachineDefinitionBuilder Behavior(
        string sourceState,
        string eventId,
        Action<SagaBehaviorDefinitionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var normalizedEventId = Required(eventId, nameof(eventId));
        if (behaviors.Any(behavior => behavior.SourceState == sourceState && behavior.EventId == normalizedEventId))
        {
            throw new ArgumentException(
                $"Saga behavior for state '{sourceState}' and event '{normalizedEventId}' is already declared.",
                nameof(eventId));
        }

        var builder = new SagaBehaviorDefinitionBuilder(sourceState, normalizedEventId);
        configure(builder);
        behaviors.Add(builder.Build());
        return this;
    }

    internal static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("The value cannot be empty or whitespace.", parameterName);
        return value;
    }
}

public sealed class SagaEventDefinitionBuilder
{
    private readonly string id;
    private readonly string messageUrn;
    private SagaCorrelationDefinition? correlation;
    private SagaCreationPolicy creationPolicy = SagaCreationPolicy.ExistingOnly;
    private SagaMissingInstancePolicy missingInstancePolicy = SagaMissingInstancePolicy.Fault;

    internal SagaEventDefinitionBuilder(string id, string messageUrn)
    {
        this.id = id;
        this.messageUrn = messageUrn;
    }

    public SagaEventDefinitionBuilder CorrelateById(string sagaMember, string messageMember)
    {
        correlation = new SagaCorrelationDefinition(
            SagaCorrelationKind.Identity,
            SagaStateMachineDefinitionBuilder.Required(sagaMember, nameof(sagaMember)),
            SagaStateMachineDefinitionBuilder.Required(messageMember, nameof(messageMember)));
        return this;
    }

    public SagaEventDefinitionBuilder CreatesIfMissing()
    {
        creationPolicy = SagaCreationPolicy.IfMissing;
        return this;
    }

    public SagaEventDefinitionBuilder ExistingOnly()
    {
        creationPolicy = SagaCreationPolicy.ExistingOnly;
        return this;
    }

    public SagaEventDefinitionBuilder DiscardIfMissing()
    {
        missingInstancePolicy = SagaMissingInstancePolicy.Discard;
        return this;
    }

    public SagaEventDefinitionBuilder FaultIfMissing()
    {
        missingInstancePolicy = SagaMissingInstancePolicy.Fault;
        return this;
    }

    internal SagaEventDefinition Build()
    {
        if (correlation is null)
            throw new InvalidOperationException($"Saga event '{id}' must declare correlation.");
        return new SagaEventDefinition(id, messageUrn, correlation, creationPolicy, missingInstancePolicy);
    }
}

public sealed class SagaBehaviorDefinitionBuilder
{
    private readonly string sourceState;
    private readonly string eventId;
    private readonly List<SagaActivityDefinition> activities = new();

    internal SagaBehaviorDefinitionBuilder(string sourceState, string eventId)
    {
        this.sourceState = sourceState;
        this.eventId = eventId;
    }

    public SagaBehaviorDefinitionBuilder Mutate(string activityId)
        => Add(new SagaActivityDefinition(
            SagaActivityKind.Mutate,
            ActivityId: SagaStateMachineDefinitionBuilder.Required(activityId, nameof(activityId))));

    public SagaBehaviorDefinitionBuilder Send<TMessage>(string destination)
        => Send(MessageUrn.For(typeof(TMessage)), destination);

    public SagaBehaviorDefinitionBuilder Send(string messageUrn, string destination)
        => Add(new SagaActivityDefinition(
            SagaActivityKind.Send,
            MessageUrn: SagaStateMachineDefinitionBuilder.Required(messageUrn, nameof(messageUrn)),
            Destination: SagaStateMachineDefinitionBuilder.Required(destination, nameof(destination))));

    public SagaBehaviorDefinitionBuilder Publish<TMessage>() => Publish(MessageUrn.For(typeof(TMessage)));

    public SagaBehaviorDefinitionBuilder Publish(string messageUrn)
        => Add(new SagaActivityDefinition(
            SagaActivityKind.Publish,
            MessageUrn: SagaStateMachineDefinitionBuilder.Required(messageUrn, nameof(messageUrn))));

    public SagaBehaviorDefinitionBuilder TransitionTo(string state)
        => Add(new SagaActivityDefinition(
            SagaActivityKind.Transition,
            TargetState: SagaStateMachineDefinitionBuilder.Required(state, nameof(state))));

    public SagaBehaviorDefinitionBuilder Finalize() => Add(new SagaActivityDefinition(SagaActivityKind.Finalize));

    public SagaBehaviorDefinitionBuilder Ignore() => Add(new SagaActivityDefinition(SagaActivityKind.Ignore));

    internal SagaBehaviorDefinition Build()
    {
        if (activities.Count == 0)
            throw new InvalidOperationException($"Saga behavior for '{sourceState}/{eventId}' must declare at least one activity.");
        return new SagaBehaviorDefinition(sourceState, eventId, activities.ToArray());
    }

    private SagaBehaviorDefinitionBuilder Add(SagaActivityDefinition activity)
    {
        if (activities.Any(existing => existing.Kind is SagaActivityKind.Transition or SagaActivityKind.Finalize or SagaActivityKind.Ignore))
            throw new InvalidOperationException("No activity can follow transition, finalize, or ignore.");
        activities.Add(activity);
        return this;
    }
}
