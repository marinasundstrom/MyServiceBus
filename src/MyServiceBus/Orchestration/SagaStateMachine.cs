using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus.Orchestration;

/// <summary>
/// Defines a saga state machine using an Automatonymous-shaped native C# DSL.
/// </summary>
public abstract class SagaStateMachine<TSaga>
    where TSaga : class
{
    private readonly string stateMachineId;
    private readonly string definitionVersion;
    private readonly string owner;
    private readonly string sagaDataUrn;
    private readonly List<State> states = new();
    private readonly List<IEventRegistration<TSaga>> events = new();
    private readonly List<IBehaviorRegistration<TSaga>> behaviors = new();
    private string stateMember = "CurrentState";
    private Func<TSaga, string?>? getState;
    private Action<TSaga, string>? setState;
    private Func<Guid, TSaga>? instanceFactory;
    private Func<TSaga, TSaga>? cloneInstance;
    private SagaCompletionPolicy completionPolicy = SagaCompletionPolicy.Retain;
    private SagaRepositoryRequirements repositoryRequirements = new(
        SagaCorrelationKind.Identity,
        SagaConcurrencyKind.SingleProcess,
        SagaDurabilityKind.Volatile,
        SagaOutboxKind.Logical);
    private SagaStateMachineDefinition? definition;
    private bool frozen;

    protected SagaStateMachine(
        string stateMachineId,
        string definitionVersion,
        string owner,
        string? sagaDataUrn = null)
    {
        this.stateMachineId = SagaStateMachineDefinitionBuilder.Required(stateMachineId, nameof(stateMachineId));
        this.definitionVersion = SagaStateMachineDefinitionBuilder.Required(definitionVersion, nameof(definitionVersion));
        this.owner = SagaStateMachineDefinitionBuilder.Required(owner, nameof(owner));
        this.sagaDataUrn = sagaDataUrn is null
            ? MessageUrn.For(typeof(TSaga))
            : SagaStateMachineDefinitionBuilder.Required(sagaDataUrn, nameof(sagaDataUrn));
    }

    public SagaStateMachineDefinition Definition
    {
        get
        {
            if (definition is not null)
                return definition;

            frozen = true;
            ValidateRuntimeConfiguration();
            var builder = new SagaStateMachineDefinitionBuilder(
                stateMachineId,
                definitionVersion,
                owner,
                sagaDataUrn,
                stateMember);
            foreach (var state in states)
                builder.State(state.Id);
            foreach (var @event in events)
                @event.Apply(builder);
            foreach (var behavior in behaviors)
                behavior.Apply(builder);
            builder.Requires(repositoryRequirements);
            if (completionPolicy == SagaCompletionPolicy.DeleteWhenFinalized)
                builder.DeleteWhenFinalized();
            definition = builder.Build();
            return definition;
        }
    }

    public SagaStateMachineRuntime<TSaga> CreateRuntime(ISagaRepository<TSaga> repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        repository.Capabilities.EnsureSupports(Definition.RepositoryRequirements, Definition.CompletionPolicy);
        var runtimeBuilder = new SagaStateMachineRuntimeBuilder<TSaga>(
            Definition,
            repository,
            instanceFactory!,
            getState!,
            setState!);
        foreach (var @event in events)
            @event.Bind(runtimeBuilder);
        foreach (var behavior in behaviors)
            behavior.Bind(runtimeBuilder);
        return runtimeBuilder.Build();
    }

    protected void InstanceState(
        Func<TSaga, string?> getter,
        Action<TSaga, string> setter,
        string stateMember = "CurrentState")
    {
        EnsureMutable();
        getState = getter ?? throw new ArgumentNullException(nameof(getter));
        setState = setter ?? throw new ArgumentNullException(nameof(setter));
        this.stateMember = SagaStateMachineDefinitionBuilder.Required(stateMember, nameof(stateMember));
    }

    protected void InstanceFactory(Func<Guid, TSaga> factory)
    {
        EnsureMutable();
        instanceFactory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    protected void CloneInstance(Func<TSaga, TSaga> clone)
    {
        EnsureMutable();
        cloneInstance = clone ?? throw new ArgumentNullException(nameof(clone));
    }

    protected State State(string id)
    {
        EnsureMutable();
        var state = new State(SagaStateMachineDefinitionBuilder.Required(id, nameof(id)));
        if (states.Any(existing => existing.Id == state.Id))
            throw new ArgumentException($"Saga state '{state.Id}' is already declared.", nameof(id));
        states.Add(state);
        return state;
    }

    protected Event<TMessage> Event<TMessage>(
        string id,
        Func<SagaEventCorrelationBuilder<TMessage>, SagaEventCorrelationBuilder<TMessage>> configure)
        where TMessage : class
        => Event(id, MessageUrn.For(typeof(TMessage)), configure);

    protected Event<TMessage> Event<TMessage>(
        string id,
        string messageUrn,
        Func<SagaEventCorrelationBuilder<TMessage>, SagaEventCorrelationBuilder<TMessage>> configure)
        where TMessage : class
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(configure);
        var @event = new Event<TMessage>(
            SagaStateMachineDefinitionBuilder.Required(id, nameof(id)),
            SagaStateMachineDefinitionBuilder.Required(messageUrn, nameof(messageUrn)));
        if (events.Any(existing => existing.Id == @event.Id))
            throw new ArgumentException($"Saga event '{@event.Id}' is already declared.", nameof(id));
        var correlation = configure(new SagaEventCorrelationBuilder<TMessage>());
        ArgumentNullException.ThrowIfNull(correlation);
        events.Add(correlation.Build<TSaga>(@event));
        return @event;
    }

    protected EventActivityBinder<TSaga, TMessage> When<TMessage>(Event<TMessage> @event)
        where TMessage : class
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(@event);
        return new EventActivityBinder<TSaga, TMessage>(@event);
    }

    protected EventActivityBinder<TSaga, TMessage> Ignore<TMessage>(Event<TMessage> @event)
        where TMessage : class
        => When(@event).Ignore();

    protected void Initially<TMessage>(EventActivityBinder<TSaga, TMessage> activity)
        where TMessage : class
        => AddBehavior(SagaStateMachineDefinition.InitialState, activity);

    protected void During<TMessage>(State state, EventActivityBinder<TSaga, TMessage> activity)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(state);
        AddBehavior(state.Id, activity);
    }

    protected void DuringAny<TMessage>(EventActivityBinder<TSaga, TMessage> activity)
        where TMessage : class
        => AddBehavior(SagaStateMachineDefinition.AnyState, activity);

    protected void DeleteWhenFinalized()
    {
        EnsureMutable();
        completionPolicy = SagaCompletionPolicy.DeleteWhenFinalized;
    }

    protected void RetainWhenFinalized()
    {
        EnsureMutable();
        completionPolicy = SagaCompletionPolicy.Retain;
    }

    protected void RepositoryRequirements(SagaRepositoryRequirements requirements)
    {
        EnsureMutable();
        repositoryRequirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
    }

    protected InMemorySagaRepository<TSaga> CreateInMemoryRepository()
    {
        frozen = true;
        ValidateRuntimeConfiguration();
        return new InMemorySagaRepository<TSaga>(cloneInstance!);
    }

    internal InMemorySagaRepository<TSaga> CreateConfiguredInMemoryRepository()
        => CreateInMemoryRepository();

    internal void RegisterConsumers<TStateMachine>(
        BusRegistrationConfigurator configurator,
        Func<IServiceProvider, SagaStateMachineRuntime<TSaga>> runtimeFactory,
        string endpointName)
        where TStateMachine : SagaStateMachine<TSaga>
    {
        foreach (var @event in events)
            @event.Register<TStateMachine>(configurator, runtimeFactory, endpointName);
    }

    private void AddBehavior<TMessage>(string sourceState, EventActivityBinder<TSaga, TMessage> activity)
        where TMessage : class
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(activity);
        if (behaviors.Any(existing => existing.SourceState == sourceState && existing.EventId == activity.Event.Id))
        {
            throw new ArgumentException(
                $"Saga behavior '{sourceState}/{activity.Event.Id}' is already declared.",
                nameof(activity));
        }
        behaviors.Add(new BehaviorRegistration<TSaga, TMessage>(sourceState, activity));
    }

    private void ValidateRuntimeConfiguration()
    {
        if (getState is null || setState is null)
            throw new InvalidOperationException("The saga state accessor must be configured.");
        if (instanceFactory is null)
            throw new InvalidOperationException("The saga instance factory must be configured.");
        if (cloneInstance is null)
            throw new InvalidOperationException("The saga clone function must be configured.");
    }

    private void EnsureMutable()
    {
        if (frozen)
            throw new InvalidOperationException("The saga state machine is frozen and cannot be changed.");
    }
}

public sealed record State(string Id);

public sealed class Event<TMessage>
    where TMessage : class
{
    internal Event(string id, string messageUrn)
    {
        Id = id;
        MessageUrn = messageUrn;
    }

    public string Id { get; }
    public string MessageUrn { get; }
}

public sealed class SagaEventCorrelationBuilder<TMessage>
    where TMessage : class
{
    private string? sagaMember;
    private string? messageMember;
    private Func<TMessage, Guid>? correlate;
    private SagaCreationPolicy creationPolicy = SagaCreationPolicy.ExistingOnly;
    private SagaMissingInstancePolicy missingPolicy = SagaMissingInstancePolicy.Fault;

    public SagaEventCorrelationBuilder<TMessage> CorrelateById(
        string sagaMember,
        string messageMember,
        Func<TMessage, Guid> correlate)
    {
        this.sagaMember = SagaStateMachineDefinitionBuilder.Required(sagaMember, nameof(sagaMember));
        this.messageMember = SagaStateMachineDefinitionBuilder.Required(messageMember, nameof(messageMember));
        this.correlate = correlate ?? throw new ArgumentNullException(nameof(correlate));
        return this;
    }

    public SagaEventCorrelationBuilder<TMessage> CreatesIfMissing()
    {
        creationPolicy = SagaCreationPolicy.IfMissing;
        return this;
    }

    public SagaEventCorrelationBuilder<TMessage> ExistingOnly()
    {
        creationPolicy = SagaCreationPolicy.ExistingOnly;
        return this;
    }

    public SagaEventCorrelationBuilder<TMessage> DiscardIfMissing()
    {
        missingPolicy = SagaMissingInstancePolicy.Discard;
        return this;
    }

    public SagaEventCorrelationBuilder<TMessage> FaultIfMissing()
    {
        missingPolicy = SagaMissingInstancePolicy.Fault;
        return this;
    }

    internal IEventRegistration<TSaga> Build<TSaga>(Event<TMessage> @event)
        where TSaga : class
    {
        if (correlate is null || sagaMember is null || messageMember is null)
            throw new InvalidOperationException($"Saga event '{@event.Id}' must declare identity correlation.");
        return new EventRegistration<TSaga, TMessage>(
            @event,
            sagaMember,
            messageMember,
            correlate,
            creationPolicy,
            missingPolicy);
    }
}

public sealed class EventActivityBinder<TSaga, TMessage>
    where TSaga : class
    where TMessage : class
{
    private readonly List<IActivityRegistration<TSaga, TMessage>> activities = new();

    internal EventActivityBinder(Event<TMessage> @event)
    {
        Event = @event;
    }

    internal Event<TMessage> Event { get; }
    internal IReadOnlyList<IActivityRegistration<TSaga, TMessage>> Activities => activities;

    public EventActivityBinder<TSaga, TMessage> Then(Action<SagaActivityContext<TSaga, TMessage>> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return Then((context, _) =>
        {
            execute(context);
            return ValueTask.CompletedTask;
        });
    }

    public EventActivityBinder<TSaga, TMessage> Then(
        Func<SagaActivityContext<TSaga, TMessage>, CancellationToken, ValueTask> execute)
        => Add(new MutateActivityRegistration<TSaga, TMessage>(execute));

    public EventActivityBinder<TSaga, TMessage> Send<TOutgoing>(
        string messageUrn,
        string destination,
        Func<SagaActivityContext<TSaga, TMessage>, TOutgoing> create)
        where TOutgoing : class
    {
        ArgumentNullException.ThrowIfNull(create);
        return Add(new MessageActivityRegistration<TSaga, TMessage, TOutgoing>(
            SagaActivityKind.Send,
            messageUrn,
            destination,
            (context, _) => ValueTask.FromResult(create(context))));
    }

    public EventActivityBinder<TSaga, TMessage> Publish<TOutgoing>(
        string messageUrn,
        Func<SagaActivityContext<TSaga, TMessage>, TOutgoing> create)
        where TOutgoing : class
    {
        ArgumentNullException.ThrowIfNull(create);
        return Add(new MessageActivityRegistration<TSaga, TMessage, TOutgoing>(
            SagaActivityKind.Publish,
            messageUrn,
            null,
            (context, _) => ValueTask.FromResult(create(context))));
    }

    public EventActivityBinder<TSaga, TMessage> TransitionTo(State state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Add(new TransitionActivityRegistration<TSaga, TMessage>(state.Id));
    }

    public EventActivityBinder<TSaga, TMessage> Finalize()
        => Add(new FinalizeActivityRegistration<TSaga, TMessage>());

    /// <summary>
    /// Finalizes the saga. This explicit alias is useful in languages where
    /// <c>Finalize</c> has special meaning.
    /// </summary>
    public EventActivityBinder<TSaga, TMessage> FinalizeSaga()
        => Finalize();

    internal EventActivityBinder<TSaga, TMessage> Ignore()
        => Add(new IgnoreActivityRegistration<TSaga, TMessage>());

    private EventActivityBinder<TSaga, TMessage> Add(IActivityRegistration<TSaga, TMessage> activity)
    {
        if (activities.Any(existing => existing.IsTerminal))
            throw new InvalidOperationException("No activity can follow transition, finalize, or ignore.");
        activities.Add(activity);
        return this;
    }
}

internal interface IEventRegistration<TSaga>
    where TSaga : class
{
    string Id { get; }
    void Apply(SagaStateMachineDefinitionBuilder builder);
    void Bind(SagaStateMachineRuntimeBuilder<TSaga> builder);
    void Register<TStateMachine>(
        BusRegistrationConfigurator configurator,
        Func<IServiceProvider, SagaStateMachineRuntime<TSaga>> runtimeFactory,
        string endpointName)
        where TStateMachine : SagaStateMachine<TSaga>;
}

internal sealed class EventRegistration<TSaga, TMessage> : IEventRegistration<TSaga>
    where TSaga : class
    where TMessage : class
{
    private readonly Event<TMessage> @event;
    private readonly string sagaMember;
    private readonly string messageMember;
    private readonly Func<TMessage, Guid> correlate;
    private readonly SagaCreationPolicy creationPolicy;
    private readonly SagaMissingInstancePolicy missingPolicy;

    public EventRegistration(
        Event<TMessage> @event,
        string sagaMember,
        string messageMember,
        Func<TMessage, Guid> correlate,
        SagaCreationPolicy creationPolicy,
        SagaMissingInstancePolicy missingPolicy)
    {
        this.@event = @event;
        this.sagaMember = sagaMember;
        this.messageMember = messageMember;
        this.correlate = correlate;
        this.creationPolicy = creationPolicy;
        this.missingPolicy = missingPolicy;
    }

    public string Id => @event.Id;

    public void Apply(SagaStateMachineDefinitionBuilder builder)
    {
        builder.Event(@event.Id, @event.MessageUrn, correlation =>
        {
            correlation.CorrelateById(sagaMember, messageMember);
            if (creationPolicy == SagaCreationPolicy.IfMissing)
                correlation.CreatesIfMissing();
            if (missingPolicy == SagaMissingInstancePolicy.Discard)
                correlation.DiscardIfMissing();
        });
    }

    public void Bind(SagaStateMachineRuntimeBuilder<TSaga> builder)
        => builder.Event(@event.Id, correlate);

    public void Register<TStateMachine>(
        BusRegistrationConfigurator configurator,
        Func<IServiceProvider, SagaStateMachineRuntime<TSaga>> runtimeFactory,
        string endpointName)
        where TStateMachine : SagaStateMachine<TSaga>
    {
        configurator.AddConsumer<SagaStateMachineConsumer<TStateMachine, TSaga, TMessage>, TMessage>(
            endpointName,
            serviceProvider =>
            {
                var runtime = runtimeFactory(serviceProvider);
                return new SagaStateMachineConsumer<TStateMachine, TSaga, TMessage>(
                    runtime,
                    runtime.Definition,
                    @event.Id,
                    correlate,
                    serviceProvider.GetRequiredService<ISendEndpointProvider>(),
                    serviceProvider.GetRequiredService<IPublishEndpoint>(),
                    serviceProvider.GetServices<IBusHook>());
            });
    }
}

internal interface IBehaviorRegistration<TSaga>
    where TSaga : class
{
    string SourceState { get; }
    string EventId { get; }
    void Apply(SagaStateMachineDefinitionBuilder builder);
    void Bind(SagaStateMachineRuntimeBuilder<TSaga> builder);
}

internal sealed class BehaviorRegistration<TSaga, TMessage> : IBehaviorRegistration<TSaga>
    where TSaga : class
    where TMessage : class
{
    private readonly EventActivityBinder<TSaga, TMessage> binder;

    public BehaviorRegistration(string sourceState, EventActivityBinder<TSaga, TMessage> binder)
    {
        SourceState = sourceState;
        this.binder = binder;
    }

    public string SourceState { get; }
    public string EventId => binder.Event.Id;

    public void Apply(SagaStateMachineDefinitionBuilder builder)
    {
        void Configure(SagaBehaviorDefinitionBuilder behavior)
        {
            for (var index = 0; index < binder.Activities.Count; index++)
                binder.Activities[index].Apply(behavior, $"{SourceState}.{EventId}.{index}");
        }

        if (SourceState == SagaStateMachineDefinition.InitialState)
            builder.Initially(EventId, Configure);
        else if (SourceState == SagaStateMachineDefinition.AnyState)
            builder.DuringAny(EventId, Configure);
        else
            builder.During(SourceState, EventId, Configure);
    }

    public void Bind(SagaStateMachineRuntimeBuilder<TSaga> builder)
    {
        for (var index = 0; index < binder.Activities.Count; index++)
            binder.Activities[index].Bind(builder, SourceState, EventId, index);
    }
}

internal interface IActivityRegistration<TSaga, TMessage>
    where TSaga : class
    where TMessage : class
{
    bool IsTerminal { get; }
    void Apply(SagaBehaviorDefinitionBuilder builder, string activityId);
    void Bind(SagaStateMachineRuntimeBuilder<TSaga> builder, string sourceState, string eventId, int index);
}

internal sealed record MutateActivityRegistration<TSaga, TMessage>(
    Func<SagaActivityContext<TSaga, TMessage>, CancellationToken, ValueTask> Execute)
    : IActivityRegistration<TSaga, TMessage>
    where TSaga : class
    where TMessage : class
{
    public bool IsTerminal => false;
    public void Apply(SagaBehaviorDefinitionBuilder builder, string activityId) => builder.Mutate(activityId);
    public void Bind(SagaStateMachineRuntimeBuilder<TSaga> builder, string sourceState, string eventId, int index)
        => builder.Mutate(sourceState, eventId, index, Execute);
}

internal sealed record MessageActivityRegistration<TSaga, TMessage, TOutgoing>(
    SagaActivityKind Kind,
    string MessageUrn,
    string? Destination,
    Func<SagaActivityContext<TSaga, TMessage>, CancellationToken, ValueTask<TOutgoing>> Create)
    : IActivityRegistration<TSaga, TMessage>
    where TSaga : class
    where TMessage : class
    where TOutgoing : class
{
    public bool IsTerminal => false;

    public void Apply(SagaBehaviorDefinitionBuilder builder, string activityId)
    {
        if (Kind == SagaActivityKind.Send)
            builder.Send(MessageUrn, Destination!);
        else
            builder.Publish(MessageUrn);
    }

    public void Bind(SagaStateMachineRuntimeBuilder<TSaga> builder, string sourceState, string eventId, int index)
        => builder.Message(sourceState, eventId, index, Create);
}

internal sealed record TransitionActivityRegistration<TSaga, TMessage>(string TargetState)
    : IActivityRegistration<TSaga, TMessage>
    where TSaga : class
    where TMessage : class
{
    public bool IsTerminal => true;
    public void Apply(SagaBehaviorDefinitionBuilder builder, string activityId) => builder.TransitionTo(TargetState);
    public void Bind(SagaStateMachineRuntimeBuilder<TSaga> builder, string sourceState, string eventId, int index) { }
}

internal sealed record FinalizeActivityRegistration<TSaga, TMessage>
    : IActivityRegistration<TSaga, TMessage>
    where TSaga : class
    where TMessage : class
{
    public bool IsTerminal => true;
    public void Apply(SagaBehaviorDefinitionBuilder builder, string activityId) => builder.Finalize();
    public void Bind(SagaStateMachineRuntimeBuilder<TSaga> builder, string sourceState, string eventId, int index) { }
}

internal sealed record IgnoreActivityRegistration<TSaga, TMessage>
    : IActivityRegistration<TSaga, TMessage>
    where TSaga : class
    where TMessage : class
{
    public bool IsTerminal => true;
    public void Apply(SagaBehaviorDefinitionBuilder builder, string activityId) => builder.Ignore();
    public void Bind(SagaStateMachineRuntimeBuilder<TSaga> builder, string sourceState, string eventId, int index) { }
}
