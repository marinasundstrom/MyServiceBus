namespace MyServiceBus.Topology;

/// <summary>
/// Immutable consumer policy captured before topology materialization.
/// </summary>
public sealed record ConsumerDefinitionModel
{
    public ConsumerDefinitionModel(Type consumerType, string? endpointName, int? concurrentMessageLimit)
    {
        ArgumentNullException.ThrowIfNull(consumerType);
        if (endpointName is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        if (concurrentMessageLimit is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(concurrentMessageLimit),
                concurrentMessageLimit,
                "The concurrent message limit must be greater than zero.");
        }

        ConsumerType = consumerType;
        EndpointName = endpointName;
        ConcurrentMessageLimit = concurrentMessageLimit;
    }

    public Type ConsumerType { get; }

    public string? EndpointName { get; }

    public int? ConcurrentMessageLimit { get; }
}
