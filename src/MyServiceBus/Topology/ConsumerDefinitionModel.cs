namespace MyServiceBus.Topology;

/// <summary>
/// Immutable consumer policy captured before topology materialization.
/// </summary>
public sealed record ConsumerDefinitionModel
{
    public ConsumerDefinitionModel(
        Type consumerType,
        EndpointDefinitionModel endpoint,
        IEnumerable<Type> messageTypes)
    {
        ArgumentNullException.ThrowIfNull(consumerType);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(messageTypes);
        var capturedMessageTypes = messageTypes.Distinct().ToArray();
        if (capturedMessageTypes.Length == 0)
            throw new ArgumentException("At least one consumed message type is required.", nameof(messageTypes));
        if (capturedMessageTypes.Any(messageType => messageType is null))
            throw new ArgumentException("Consumed message types must not contain null.", nameof(messageTypes));

        ConsumerType = consumerType;
        Endpoint = endpoint;
        MessageTypes = capturedMessageTypes;
    }

    public Type ConsumerType { get; }

    public EndpointDefinitionModel Endpoint { get; }

    public string EndpointName => Endpoint.Name;

    public bool EndpointNameIsExplicit => Endpoint.NameIsExplicit;

    public Type? EndpointNameFormatterType => Endpoint.NameFormatterType;

    public IReadOnlyList<Type> MessageTypes { get; }

    public int? ConcurrentMessageLimit => Endpoint.ConcurrentMessageLimit;

    public ushort? PrefetchCount => Endpoint.PrefetchCount;
}
