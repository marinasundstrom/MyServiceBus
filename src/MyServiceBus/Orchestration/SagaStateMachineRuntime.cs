using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace MyServiceBus.Orchestration;

/// <summary>
/// Executes a normalized saga state-machine definition against a volatile in-memory repository.
/// </summary>
/// <typeparam name="TSaga">The application-owned saga data type.</typeparam>
public sealed class SagaStateMachineRuntime<TSaga>
    where TSaga : class
{
    private readonly SagaStateMachineDefinition definition;
    private readonly InMemorySagaRepository<TSaga> repository;
    private readonly Func<Guid, TSaga> instanceFactory;
    private readonly Func<TSaga, string?> getState;
    private readonly Action<TSaga, string> setState;
    private readonly IReadOnlyDictionary<Type, SagaEventRuntimeBinding<TSaga>> events;
    private readonly IReadOnlyDictionary<SagaActivityAddress, SagaActivityRuntimeBinding<TSaga>> activities;

    internal SagaStateMachineRuntime(
        SagaStateMachineDefinition definition,
        InMemorySagaRepository<TSaga> repository,
        Func<Guid, TSaga> instanceFactory,
        Func<TSaga, string?> getState,
        Action<TSaga, string> setState,
        IReadOnlyDictionary<Type, SagaEventRuntimeBinding<TSaga>> events,
        IReadOnlyDictionary<SagaActivityAddress, SagaActivityRuntimeBinding<TSaga>> activities)
    {
        this.definition = definition;
        this.repository = repository;
        this.instanceFactory = instanceFactory;
        this.getState = getState;
        this.setState = setState;
        this.events = events;
        this.activities = activities;
    }

    public ValueTask<SagaDeliveryResult> Deliver<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default)
        where TMessage : class
        => Deliver(message, typeof(TMessage), cancellationToken);

    private async ValueTask<SagaDeliveryResult> Deliver(
        object message,
        Type messageType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!events.TryGetValue(messageType, out var eventBinding))
            throw new ArgumentException($"Message type '{messageType}' is not bound to the saga state machine.", nameof(message));

        var correlationId = eventBinding.Correlate(message);
        if (correlationId == Guid.Empty)
            throw new SagaCorrelationException(definition.StateMachineId, eventBinding.Event.Id, "The correlation ID cannot be empty.");

        return await repository.Execute(
            correlationId,
            async storedInstance =>
            {
                var created = storedInstance is null;
                if (created && eventBinding.Event.CreationPolicy == SagaCreationPolicy.ExistingOnly)
                {
                    if (eventBinding.Event.MissingInstancePolicy == SagaMissingInstancePolicy.Discard)
                    {
                        return SagaRepositoryTransaction<TSaga, SagaDeliveryResult>.NoChange(
                            new SagaDeliveryResult(
                                SagaDeliveryStatus.MissingDiscarded,
                                correlationId,
                                null,
                                null,
                                false,
                                false,
                                false,
                                Array.Empty<SagaOutgoingOperation>()));
                    }

                    throw new SagaMissingInstanceException(
                        definition.StateMachineId,
                        eventBinding.Event.Id,
                        correlationId);
                }

                var instance = storedInstance ?? instanceFactory(correlationId);
                var beginState = NormalizeState(getState(instance));
                var behavior = SelectBehavior(beginState, eventBinding.Event.Id);
                if (behavior is null)
                {
                    throw new SagaEventNotAcceptedException(
                        definition.StateMachineId,
                        eventBinding.Event.Id,
                        correlationId,
                        beginState);
                }

                if (behavior.Activities.Count == 1 && behavior.Activities[0].Kind == SagaActivityKind.Ignore)
                {
                    return SagaRepositoryTransaction<TSaga, SagaDeliveryResult>.NoChange(
                        new SagaDeliveryResult(
                            SagaDeliveryStatus.Ignored,
                            correlationId,
                            beginState,
                            beginState,
                            false,
                            beginState == SagaStateMachineDefinition.FinalState,
                            storedInstance is not null,
                            Array.Empty<SagaOutgoingOperation>()));
                }

                var outgoing = new List<SagaOutgoingOperation>();
                for (var index = 0; index < behavior.Activities.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var activity = behavior.Activities[index];
                    switch (activity.Kind)
                    {
                        case SagaActivityKind.Mutate:
                            await ExecuteBoundActivity(
                                behavior,
                                index,
                                instance,
                                message,
                                correlationId,
                                outgoing,
                                cancellationToken).ConfigureAwait(false);
                            break;
                        case SagaActivityKind.Send:
                        case SagaActivityKind.Publish:
                            await ExecuteBoundActivity(
                                behavior,
                                index,
                                instance,
                                message,
                                correlationId,
                                outgoing,
                                cancellationToken).ConfigureAwait(false);
                            break;
                        case SagaActivityKind.Transition:
                            setState(instance, activity.TargetState!);
                            break;
                        case SagaActivityKind.Finalize:
                            setState(instance, SagaStateMachineDefinition.FinalState);
                            break;
                        case SagaActivityKind.Ignore:
                            throw new InvalidOperationException("Ignore cannot be combined with executable activities.");
                    }
                }

                var endState = NormalizeState(getState(instance));
                var completed = endState == SagaStateMachineDefinition.FinalState;
                var delete = completed && definition.CompletionPolicy == SagaCompletionPolicy.DeleteWhenFinalized;
                var result = new SagaDeliveryResult(
                    SagaDeliveryStatus.Consumed,
                    correlationId,
                    beginState,
                    endState,
                    created,
                    completed,
                    !delete,
                    outgoing.ToArray());

                return delete
                    ? SagaRepositoryTransaction<TSaga, SagaDeliveryResult>.Delete(result)
                    : SagaRepositoryTransaction<TSaga, SagaDeliveryResult>.Upsert(instance, result);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private SagaBehaviorDefinition? SelectBehavior(string state, string eventId)
    {
        var exact = definition.Behaviors.FirstOrDefault(behavior =>
            behavior.SourceState == state && behavior.EventId == eventId);
        if (exact is not null || state is SagaStateMachineDefinition.InitialState or SagaStateMachineDefinition.FinalState)
            return exact;

        return definition.Behaviors.FirstOrDefault(behavior =>
            behavior.SourceState == SagaStateMachineDefinition.AnyState && behavior.EventId == eventId);
    }

    private async ValueTask ExecuteBoundActivity(
        SagaBehaviorDefinition behavior,
        int index,
        TSaga instance,
        object message,
        Guid correlationId,
        List<SagaOutgoingOperation> outgoing,
        CancellationToken cancellationToken)
    {
        var address = new SagaActivityAddress(behavior.SourceState, behavior.EventId, index);
        if (!activities.TryGetValue(address, out var binding))
            throw new InvalidOperationException($"Saga activity '{address}' has no executable binding.");

        await binding.Execute(
            instance,
            message,
            correlationId,
            outgoing,
            cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeState(string? state)
        => string.IsNullOrWhiteSpace(state) ? SagaStateMachineDefinition.InitialState : state;
}

public sealed class SagaStateMachineRuntimeBuilder<TSaga>
    where TSaga : class
{
    private readonly SagaStateMachineDefinition definition;
    private readonly InMemorySagaRepository<TSaga> repository;
    private readonly Func<Guid, TSaga> instanceFactory;
    private readonly Func<TSaga, string?> getState;
    private readonly Action<TSaga, string> setState;
    private readonly Dictionary<Type, SagaEventRuntimeBinding<TSaga>> events = new();
    private readonly Dictionary<SagaActivityAddress, SagaActivityRuntimeBinding<TSaga>> activities = new();

    public SagaStateMachineRuntimeBuilder(
        SagaStateMachineDefinition definition,
        InMemorySagaRepository<TSaga> repository,
        Func<Guid, TSaga> instanceFactory,
        Func<TSaga, string?> getState,
        Action<TSaga, string> setState)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.instanceFactory = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));
        this.getState = getState ?? throw new ArgumentNullException(nameof(getState));
        this.setState = setState ?? throw new ArgumentNullException(nameof(setState));
    }

    public SagaStateMachineRuntimeBuilder<TSaga> Event<TMessage>(
        string eventId,
        Func<TMessage, Guid> correlate)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(correlate);
        var definitionEvent = FindEvent(eventId);
        if (events.ContainsKey(typeof(TMessage)))
            throw new ArgumentException($"Message type '{typeof(TMessage)}' is already bound.", nameof(eventId));
        events.Add(
            typeof(TMessage),
            new SagaEventRuntimeBinding<TSaga>(definitionEvent, message => correlate((TMessage)message)));
        return this;
    }

    public SagaStateMachineRuntimeBuilder<TSaga> Mutate<TMessage>(
        string sourceState,
        string eventId,
        int activityIndex,
        Func<SagaActivityContext<TSaga, TMessage>, CancellationToken, ValueTask> execute)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(execute);
        return BindActivity(
            sourceState,
            eventId,
            activityIndex,
            SagaActivityKind.Mutate,
            (saga, message, correlationId, _, cancellationToken) =>
                execute(new SagaActivityContext<TSaga, TMessage>(saga, (TMessage)message, correlationId), cancellationToken));
    }

    public SagaStateMachineRuntimeBuilder<TSaga> Message<TIncoming, TOutgoing>(
        string sourceState,
        string eventId,
        int activityIndex,
        Func<SagaActivityContext<TSaga, TIncoming>, CancellationToken, ValueTask<TOutgoing>> create)
        where TIncoming : class
        where TOutgoing : class
    {
        ArgumentNullException.ThrowIfNull(create);
        var descriptor = FindActivity(sourceState, eventId, activityIndex);
        if (descriptor.Kind is not (SagaActivityKind.Send or SagaActivityKind.Publish))
            throw new ArgumentException("The selected activity is not a send or publish operation.", nameof(activityIndex));

        return BindActivity(
            sourceState,
            eventId,
            activityIndex,
            descriptor.Kind,
            async (saga, message, correlationId, outgoing, cancellationToken) =>
            {
                var outboundMessage = await create(
                    new SagaActivityContext<TSaga, TIncoming>(saga, (TIncoming)message, correlationId),
                    cancellationToken).ConfigureAwait(false);
                ArgumentNullException.ThrowIfNull(outboundMessage);
                outgoing.Add(new SagaOutgoingOperation(
                    descriptor.Kind,
                    descriptor.MessageUrn!,
                    descriptor.Destination,
                    outboundMessage));
            });
    }

    public SagaStateMachineRuntime<TSaga> Build()
    {
        definition.Validate();
        foreach (var @event in definition.Events)
        {
            if (events.Values.All(binding => binding.Event.Id != @event.Id))
                throw new InvalidOperationException($"Saga event '{@event.Id}' has no runtime message binding.");
        }

        foreach (var behavior in definition.Behaviors)
        {
            for (var index = 0; index < behavior.Activities.Count; index++)
            {
                var activity = behavior.Activities[index];
                if (activity.Kind is SagaActivityKind.Mutate or SagaActivityKind.Send or SagaActivityKind.Publish)
                {
                    var address = new SagaActivityAddress(behavior.SourceState, behavior.EventId, index);
                    if (!activities.ContainsKey(address))
                        throw new InvalidOperationException($"Saga activity '{address}' has no executable binding.");
                }
            }
        }

        return new SagaStateMachineRuntime<TSaga>(
            definition,
            repository,
            instanceFactory,
            getState,
            setState,
            new Dictionary<Type, SagaEventRuntimeBinding<TSaga>>(events),
            new Dictionary<SagaActivityAddress, SagaActivityRuntimeBinding<TSaga>>(activities));
    }

    private SagaStateMachineRuntimeBuilder<TSaga> BindActivity(
        string sourceState,
        string eventId,
        int activityIndex,
        SagaActivityKind expectedKind,
        SagaActivityExecutor<TSaga> execute)
    {
        var descriptor = FindActivity(sourceState, eventId, activityIndex);
        if (descriptor.Kind != expectedKind)
            throw new ArgumentException($"The selected activity is '{descriptor.Kind}', not '{expectedKind}'.", nameof(activityIndex));
        var address = new SagaActivityAddress(sourceState, eventId, activityIndex);
        if (!activities.TryAdd(address, new SagaActivityRuntimeBinding<TSaga>(execute)))
            throw new ArgumentException($"Saga activity '{address}' is already bound.", nameof(activityIndex));
        return this;
    }

    private SagaEventDefinition FindEvent(string eventId)
        => definition.Events.FirstOrDefault(@event => @event.Id == eventId)
           ?? throw new ArgumentException($"Saga event '{eventId}' is not declared.", nameof(eventId));

    private SagaActivityDefinition FindActivity(string sourceState, string eventId, int activityIndex)
    {
        var behavior = definition.Behaviors.FirstOrDefault(candidate =>
            candidate.SourceState == sourceState && candidate.EventId == eventId);
        if (behavior is null)
            throw new ArgumentException($"Saga behavior '{sourceState}/{eventId}' is not declared.", nameof(eventId));
        if (activityIndex < 0 || activityIndex >= behavior.Activities.Count)
            throw new ArgumentOutOfRangeException(nameof(activityIndex));
        return behavior.Activities[activityIndex];
    }
}

public readonly record struct SagaActivityContext<TSaga, TMessage>(
    TSaga Saga,
    TMessage Message,
    Guid CorrelationId)
    where TSaga : class
    where TMessage : class;

public sealed record SagaDeliveryResult(
    [property: JsonPropertyName("status")] SagaDeliveryStatus Status,
    [property: JsonPropertyName("correlationId")] Guid CorrelationId,
    [property: JsonPropertyName("beginState")] string? BeginState,
    [property: JsonPropertyName("endState")] string? EndState,
    [property: JsonPropertyName("created")] bool Created,
    [property: JsonPropertyName("completed")] bool Completed,
    [property: JsonPropertyName("instancePresent")] bool InstancePresent,
    [property: JsonPropertyName("outgoing")] IReadOnlyList<SagaOutgoingOperation> Outgoing);

public sealed record SagaOutgoingOperation(
    [property: JsonPropertyName("kind")] SagaActivityKind Kind,
    [property: JsonPropertyName("messageUrn")] string MessageUrn,
    [property: JsonPropertyName("destination")] string? Destination,
    [property: JsonIgnore] object Message);

[JsonConverter(typeof(JsonStringEnumConverter<SagaDeliveryStatus>))]
public enum SagaDeliveryStatus
{
    [JsonStringEnumMemberName("consumed")]
    Consumed,

    [JsonStringEnumMemberName("ignored")]
    Ignored,

    [JsonStringEnumMemberName("missing-discarded")]
    MissingDiscarded
}

public sealed class SagaCorrelationException : Exception
{
    public SagaCorrelationException(string stateMachineId, string eventId, string message)
        : base($"Saga state machine '{stateMachineId}' could not correlate event '{eventId}': {message}")
    {
    }
}

public sealed class SagaMissingInstanceException : Exception
{
    public SagaMissingInstanceException(string stateMachineId, string eventId, Guid correlationId)
        : base($"Saga state machine '{stateMachineId}' has no instance '{correlationId}' for event '{eventId}'.")
    {
    }
}

public sealed class SagaEventNotAcceptedException : Exception
{
    public SagaEventNotAcceptedException(
        string stateMachineId,
        string eventId,
        Guid correlationId,
        string state)
        : base($"Saga state machine '{stateMachineId}' did not accept event '{eventId}' for instance '{correlationId}' in state '{state}'.")
    {
    }
}

internal readonly record struct SagaActivityAddress(string SourceState, string EventId, int ActivityIndex)
{
    public override string ToString() => $"{SourceState}/{EventId}[{ActivityIndex}]";
}

internal sealed record SagaEventRuntimeBinding<TSaga>(
    SagaEventDefinition Event,
    Func<object, Guid> Correlate)
    where TSaga : class;

internal sealed record SagaActivityRuntimeBinding<TSaga>(SagaActivityExecutor<TSaga> Execute)
    where TSaga : class;

internal delegate ValueTask SagaActivityExecutor<TSaga>(
    TSaga saga,
    object message,
    Guid correlationId,
    List<SagaOutgoingOperation> outgoing,
    CancellationToken cancellationToken)
    where TSaga : class;

/// <summary>
/// Volatile, process-local saga storage with per-instance transactional mutation.
/// </summary>
public sealed class InMemorySagaRepository<TSaga>
    where TSaga : class
{
    private readonly ConcurrentDictionary<Guid, TSaga> instances = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> locks = new();
    private readonly Func<TSaga, TSaga> clone;

    public InMemorySagaRepository(Func<TSaga, TSaga> clone)
    {
        this.clone = clone ?? throw new ArgumentNullException(nameof(clone));
    }

    public int Count => instances.Count;

    public bool TryGet(Guid correlationId, out TSaga? instance)
    {
        if (instances.TryGetValue(correlationId, out var stored))
        {
            instance = clone(stored);
            return true;
        }

        instance = null;
        return false;
    }

    internal async ValueTask<TResult> Execute<TResult>(
        Guid correlationId,
        Func<TSaga?, ValueTask<SagaRepositoryTransaction<TSaga, TResult>>> execute,
        CancellationToken cancellationToken)
    {
        var instanceLock = locks.GetOrAdd(correlationId, static _ => new SemaphoreSlim(1, 1));
        await instanceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var workingCopy = instances.TryGetValue(correlationId, out var stored) ? clone(stored) : null;
            var transaction = await execute(workingCopy).ConfigureAwait(false);
            switch (transaction.Mutation)
            {
                case SagaRepositoryMutation.Upsert:
                    instances[correlationId] = clone(transaction.Instance!);
                    break;
                case SagaRepositoryMutation.Delete:
                    instances.TryRemove(correlationId, out _);
                    break;
                case SagaRepositoryMutation.None:
                    break;
            }
            return transaction.Result;
        }
        finally
        {
            instanceLock.Release();
        }
    }
}

internal sealed record SagaRepositoryTransaction<TSaga, TResult>(
    SagaRepositoryMutation Mutation,
    TSaga? Instance,
    TResult Result)
    where TSaga : class
{
    public static SagaRepositoryTransaction<TSaga, TResult> NoChange(TResult result)
        => new(SagaRepositoryMutation.None, null, result);

    public static SagaRepositoryTransaction<TSaga, TResult> Upsert(TSaga instance, TResult result)
        => new(SagaRepositoryMutation.Upsert, instance, result);

    public static SagaRepositoryTransaction<TSaga, TResult> Delete(TResult result)
        => new(SagaRepositoryMutation.Delete, null, result);
}

internal enum SagaRepositoryMutation
{
    None,
    Upsert,
    Delete
}
