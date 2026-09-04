namespace MyServiceBus.Topology;

/// <summary>
/// Immutable consumer policy captured before topology materialization.
/// </summary>
public sealed record ConsumerDefinitionModel
{
    public ConsumerDefinitionModel(
        Type consumerType,
        string endpointName,
        bool endpointNameIsExplicit,
        Type? endpointNameFormatterType,
        IEnumerable<Type> messageTypes,
        int? concurrentMessageLimit)
    {
        ArgumentNullException.ThrowIfNull(consumerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        ArgumentNullException.ThrowIfNull(messageTypes);
        if (concurrentMessageLimit is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(concurrentMessageLimit),
                concurrentMessageLimit,
                "The concurrent message limit must be greater than zero.");
        }

        var capturedMessageTypes = messageTypes.Distinct().ToArray();
        if (capturedMessageTypes.Length == 0)
            throw new ArgumentException("At least one consumed message type is required.", nameof(messageTypes));
        if (capturedMessageTypes.Any(messageType => messageType is null))
            throw new ArgumentException("Consumed message types must not contain null.", nameof(messageTypes));

        ConsumerType = consumerType;
        EndpointName = endpointName;
        EndpointNameIsExplicit = endpointNameIsExplicit;
        EndpointNameFormatterType = endpointNameFormatterType;
        MessageTypes = capturedMessageTypes;
        ConcurrentMessageLimit = concurrentMessageLimit;
    }

    public Type ConsumerType { get; }

    public string EndpointName { get; }

    public bool EndpointNameIsExplicit { get; }

    public Type? EndpointNameFormatterType { get; }

    public IReadOnlyList<Type> MessageTypes { get; }

    public int? ConcurrentMessageLimit { get; }
}
