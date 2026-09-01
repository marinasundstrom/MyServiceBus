using System.Text.Json.Serialization;

namespace MyServiceBus.Orchestration;

/// <summary>
/// Describes a portable saga state-machine definition without executable callbacks.
/// </summary>
public sealed record SagaStateMachineDefinition(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("stateMachineId")] string StateMachineId,
    [property: JsonPropertyName("definitionVersion")] string DefinitionVersion,
    [property: JsonPropertyName("owner")] string Owner,
    [property: JsonPropertyName("sagaDataUrn")] string SagaDataUrn,
    [property: JsonPropertyName("stateMember")] string StateMember,
    [property: JsonPropertyName("completionPolicy")] SagaCompletionPolicy CompletionPolicy,
    [property: JsonPropertyName("repositoryRequirements")] SagaRepositoryRequirements RepositoryRequirements,
    [property: JsonPropertyName("states")] IReadOnlyList<SagaStateDefinition> States,
    [property: JsonPropertyName("events")] IReadOnlyList<SagaEventDefinition> Events,
    [property: JsonPropertyName("behaviors")] IReadOnlyList<SagaBehaviorDefinition> Behaviors)
{
    public const int CurrentSchemaVersion = 1;
    public const string InitialState = "Initial";
    public const string FinalState = "Final";
    public const string AnyState = "Any";

    /// <summary>
    /// Validates the portable declaration independently of runtime registration.
    /// </summary>
    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
            throw new InvalidOperationException($"Unsupported saga state-machine schema version {SchemaVersion}; expected {CurrentSchemaVersion}.");

        Required(StateMachineId, nameof(StateMachineId));
        Required(DefinitionVersion, nameof(DefinitionVersion));
        Required(Owner, nameof(Owner));
        Required(SagaDataUrn, nameof(SagaDataUrn));
        Required(StateMember, nameof(StateMember));

        if (!Enum.IsDefined(CompletionPolicy))
            throw new InvalidOperationException("The saga completion policy is unknown.");
        ArgumentNullException.ThrowIfNull(RepositoryRequirements);
        RepositoryRequirements.Validate();

        if (States is null || States.Count == 0)
            throw new InvalidOperationException("A saga state machine must declare at least one ordinary state.");
        if (Events is null || Events.Count == 0)
            throw new InvalidOperationException("A saga state machine must declare at least one event.");
        if (Behaviors is null || Behaviors.Count == 0)
            throw new InvalidOperationException("A saga state machine must declare at least one behavior.");

        var stateIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in States)
        {
            ArgumentNullException.ThrowIfNull(state);
            Required(state.Id, nameof(SagaStateDefinition.Id));
            if (IsReservedState(state.Id))
                throw new InvalidOperationException($"Saga state '{state.Id}' uses a reserved state identity.");
            if (!stateIds.Add(state.Id))
                throw new InvalidOperationException($"Saga state '{state.Id}' is declared more than once.");
        }

        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var @event in Events)
        {
            ArgumentNullException.ThrowIfNull(@event);
            Required(@event.Id, nameof(SagaEventDefinition.Id));
            Required(@event.MessageUrn, nameof(SagaEventDefinition.MessageUrn));
            if (!eventIds.Add(@event.Id))
                throw new InvalidOperationException($"Saga event '{@event.Id}' is declared more than once.");
            if (!Enum.IsDefined(@event.CreationPolicy) || !Enum.IsDefined(@event.MissingInstancePolicy))
                throw new InvalidOperationException($"Saga event '{@event.Id}' contains an unknown policy.");
            ArgumentNullException.ThrowIfNull(@event.Correlation);
            @event.Correlation.Validate(@event.Id);
        }

        var behaviorKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var behavior in Behaviors)
        {
            ArgumentNullException.ThrowIfNull(behavior);
            Required(behavior.SourceState, nameof(SagaBehaviorDefinition.SourceState));
            Required(behavior.EventId, nameof(SagaBehaviorDefinition.EventId));
            if (behavior.SourceState != InitialState && behavior.SourceState != AnyState && !stateIds.Contains(behavior.SourceState))
                throw new InvalidOperationException($"Saga behavior references unknown source state '{behavior.SourceState}'.");
            if (!eventIds.Contains(behavior.EventId))
                throw new InvalidOperationException($"Saga behavior references unknown event '{behavior.EventId}'.");
            if (!behaviorKeys.Add($"{behavior.SourceState}\u001f{behavior.EventId}"))
                throw new InvalidOperationException($"Saga behavior for state '{behavior.SourceState}' and event '{behavior.EventId}' is declared more than once.");
            ValidateActivities(behavior, stateIds);
        }

        foreach (var @event in Events)
        {
            var hasInitialBehavior = Behaviors.Any(behavior =>
                behavior.SourceState == InitialState && behavior.EventId == @event.Id);
            if (@event.CreationPolicy == SagaCreationPolicy.IfMissing && !hasInitialBehavior)
                throw new InvalidOperationException($"Creating saga event '{@event.Id}' must declare an Initial behavior.");
            if (@event.CreationPolicy == SagaCreationPolicy.ExistingOnly && hasInitialBehavior)
                throw new InvalidOperationException($"Initial saga event '{@event.Id}' must permit instance creation.");
        }
    }

    private static void ValidateActivities(SagaBehaviorDefinition behavior, HashSet<string> stateIds)
    {
        if (behavior.Activities is null || behavior.Activities.Count == 0)
            throw new InvalidOperationException($"Saga behavior for '{behavior.SourceState}/{behavior.EventId}' must declare at least one activity.");

        for (var index = 0; index < behavior.Activities.Count; index++)
        {
            var activity = behavior.Activities[index];
            ArgumentNullException.ThrowIfNull(activity);
            if (!Enum.IsDefined(activity.Kind))
                throw new InvalidOperationException("A saga behavior contains an unknown activity kind.");

            var isLast = index == behavior.Activities.Count - 1;
            switch (activity.Kind)
            {
                case SagaActivityKind.Mutate:
                    Required(activity.ActivityId, nameof(SagaActivityDefinition.ActivityId));
                    RequireEmpty(activity.MessageUrn, activity.Destination, activity.TargetState, activity.Kind);
                    break;
                case SagaActivityKind.Send:
                    Required(activity.MessageUrn, nameof(SagaActivityDefinition.MessageUrn));
                    Required(activity.Destination, nameof(SagaActivityDefinition.Destination));
                    RequireEmpty(activity.ActivityId, activity.TargetState, null, activity.Kind);
                    break;
                case SagaActivityKind.Publish:
                    Required(activity.MessageUrn, nameof(SagaActivityDefinition.MessageUrn));
                    RequireEmpty(activity.ActivityId, activity.Destination, activity.TargetState, activity.Kind);
                    break;
                case SagaActivityKind.Transition:
                    var targetState = Required(activity.TargetState, nameof(SagaActivityDefinition.TargetState));
                    if (!stateIds.Contains(targetState))
                        throw new InvalidOperationException($"Saga transition targets unknown state '{targetState}'.");
                    RequireEmpty(activity.ActivityId, activity.MessageUrn, activity.Destination, activity.Kind);
                    if (!isLast)
                        throw new InvalidOperationException("A saga transition must be the final activity in its behavior.");
                    break;
                case SagaActivityKind.Finalize:
                case SagaActivityKind.Ignore:
                    RequireEmpty(activity.ActivityId, activity.MessageUrn, activity.Destination, activity.Kind, activity.TargetState);
                    if (behavior.Activities.Count != 1 && activity.Kind == SagaActivityKind.Ignore)
                        throw new InvalidOperationException("An ignored saga behavior cannot declare other activities.");
                    if (!isLast)
                        throw new InvalidOperationException($"Saga activity '{activity.Kind}' must be the final activity in its behavior.");
                    break;
            }
        }
    }

    private static bool IsReservedState(string state) => state is InitialState or FinalState or AnyState;

    private static void RequireEmpty(
        string? first,
        string? second,
        string? third,
        SagaActivityKind kind,
        string? fourth = null)
    {
        if (first is not null || second is not null || third is not null || fourth is not null)
            throw new InvalidOperationException($"Saga activity '{kind}' contains fields that do not apply to it.");
    }

    internal static string Required(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Saga state-machine field '{field}' cannot be empty or whitespace.");
        return value;
    }
}

public sealed record SagaRepositoryRequirements(
    [property: JsonPropertyName("correlation")] SagaCorrelationKind Correlation,
    [property: JsonPropertyName("concurrency")] SagaConcurrencyKind Concurrency,
    [property: JsonPropertyName("durability")] SagaDurabilityKind Durability,
    [property: JsonPropertyName("outbox")] SagaOutboxKind Outbox)
{
    internal void Validate()
    {
        if (!Enum.IsDefined(Correlation) || !Enum.IsDefined(Concurrency) ||
            !Enum.IsDefined(Durability) || !Enum.IsDefined(Outbox))
        {
            throw new InvalidOperationException("The saga repository requirements contain an unknown capability.");
        }
    }
}

public sealed record SagaStateDefinition([property: JsonPropertyName("id")] string Id);

public sealed record SagaEventDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("messageUrn")] string MessageUrn,
    [property: JsonPropertyName("correlation")] SagaCorrelationDefinition Correlation,
    [property: JsonPropertyName("creationPolicy")] SagaCreationPolicy CreationPolicy,
    [property: JsonPropertyName("missingInstancePolicy")] SagaMissingInstancePolicy MissingInstancePolicy);

public sealed record SagaCorrelationDefinition(
    [property: JsonPropertyName("kind")] SagaCorrelationKind Kind,
    [property: JsonPropertyName("sagaMember")] string SagaMember,
    [property: JsonPropertyName("messageMember")] string MessageMember)
{
    internal void Validate(string eventId)
    {
        if (Kind != SagaCorrelationKind.Identity)
            throw new InvalidOperationException($"Saga event '{eventId}' uses unsupported correlation kind '{Kind}'.");
        SagaStateMachineDefinition.Required(SagaMember, nameof(SagaMember));
        SagaStateMachineDefinition.Required(MessageMember, nameof(MessageMember));
    }
}

public sealed record SagaBehaviorDefinition(
    [property: JsonPropertyName("sourceState")] string SourceState,
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("activities")] IReadOnlyList<SagaActivityDefinition> Activities);

public sealed record SagaActivityDefinition(
    [property: JsonPropertyName("kind")] SagaActivityKind Kind,
    [property: JsonPropertyName("activityId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ActivityId = null,
    [property: JsonPropertyName("messageUrn"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MessageUrn = null,
    [property: JsonPropertyName("destination"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Destination = null,
    [property: JsonPropertyName("targetState"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetState = null);

[JsonConverter(typeof(JsonStringEnumConverter<SagaCompletionPolicy>))]
public enum SagaCompletionPolicy
{
    [JsonStringEnumMemberName("retain")]
    Retain,

    [JsonStringEnumMemberName("delete-when-finalized")]
    DeleteWhenFinalized
}

[JsonConverter(typeof(JsonStringEnumConverter<SagaCorrelationKind>))]
public enum SagaCorrelationKind
{
    [JsonStringEnumMemberName("identity")]
    Identity
}

[JsonConverter(typeof(JsonStringEnumConverter<SagaCreationPolicy>))]
public enum SagaCreationPolicy
{
    [JsonStringEnumMemberName("existing-only")]
    ExistingOnly,

    [JsonStringEnumMemberName("if-missing")]
    IfMissing
}

[JsonConverter(typeof(JsonStringEnumConverter<SagaMissingInstancePolicy>))]
public enum SagaMissingInstancePolicy
{
    [JsonStringEnumMemberName("discard")]
    Discard,

    [JsonStringEnumMemberName("fault")]
    Fault
}

[JsonConverter(typeof(JsonStringEnumConverter<SagaConcurrencyKind>))]
public enum SagaConcurrencyKind
{
    [JsonStringEnumMemberName("single-process")]
    SingleProcess,

    [JsonStringEnumMemberName("optimistic")]
    Optimistic,

    [JsonStringEnumMemberName("pessimistic")]
    Pessimistic
}

[JsonConverter(typeof(JsonStringEnumConverter<SagaDurabilityKind>))]
public enum SagaDurabilityKind
{
    [JsonStringEnumMemberName("volatile")]
    Volatile,

    [JsonStringEnumMemberName("durable")]
    Durable
}

[JsonConverter(typeof(JsonStringEnumConverter<SagaOutboxKind>))]
public enum SagaOutboxKind
{
    [JsonStringEnumMemberName("logical")]
    Logical,

    [JsonStringEnumMemberName("transactional")]
    Transactional
}

[JsonConverter(typeof(JsonStringEnumConverter<SagaActivityKind>))]
public enum SagaActivityKind
{
    [JsonStringEnumMemberName("mutate")]
    Mutate,

    [JsonStringEnumMemberName("send")]
    Send,

    [JsonStringEnumMemberName("publish")]
    Publish,

    [JsonStringEnumMemberName("transition")]
    Transition,

    [JsonStringEnumMemberName("finalize")]
    Finalize,

    [JsonStringEnumMemberName("ignore")]
    Ignore
}
