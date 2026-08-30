using MyServiceBus.Topology;

namespace MyServiceBus;

internal sealed record AmazonSqsReceiveEndpointTopology(
    string QueueName,
    bool Durable,
    bool Temporary,
    int PrefetchCount,
    IReadOnlyList<MessageBinding> Bindings,
    int ConcurrentMessageLimit)
{
    public static AmazonSqsReceiveEndpointTopology Project(ReceiveEndpointTransportTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        if (topology.Durable && topology.Temporary)
            throw new ArgumentException("An Amazon SQS endpoint cannot be both durable and temporary.", nameof(topology));
        AmazonSqsEntityName.Validate(topology.Name);
        if (topology.Bindings.Count == 0)
            throw new ArgumentException("An Amazon SQS receive endpoint must have at least one binding.", nameof(topology));
        foreach (var binding in topology.Bindings)
            AmazonSqsEntityName.ValidateTopic(binding.EntityName);
        if (topology.TransportOptions is { Count: > 0 })
            throw new NotSupportedException("Amazon SQS transport options are not supported in the first standard-queue slice.");
        return new AmazonSqsReceiveEndpointTopology(
            topology.Name, topology.Durable, topology.Temporary, topology.PrefetchCount,
            topology.Bindings.ToArray(), topology.ConcurrentMessageLimit);
    }
}
