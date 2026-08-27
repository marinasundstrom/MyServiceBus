using MyServiceBus.Topology;

namespace MyServiceBus.AzureServiceBus;

internal sealed record AzureServiceBusReceiveEndpointTopology(
    string QueueName,
    bool Durable,
    bool Temporary,
    int PrefetchCount,
    IReadOnlyList<MessageBinding> Bindings)
{
    public static AzureServiceBusReceiveEndpointTopology Project(ReceiveEndpointTransportTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        if (topology.Durable && topology.Temporary)
            throw new ArgumentException("An Azure Service Bus endpoint cannot be both durable and temporary.", nameof(topology));
        if (string.IsNullOrWhiteSpace(topology.Name))
            throw new ArgumentException("Azure Service Bus queue name cannot be blank.", nameof(topology));
        if (topology.Bindings.Count == 0)
            throw new ArgumentException("Azure Service Bus receive endpoint must have at least one binding.", nameof(topology));
        if (topology.Bindings.Any(binding => string.IsNullOrWhiteSpace(binding.EntityName)))
            throw new ArgumentException("Azure Service Bus topic binding name cannot be blank.", nameof(topology));
        if (topology.TransportOptions is { Count: > 0 })
            throw new NotSupportedException("Azure Service Bus transport options are not supported in the first preview slice.");

        return new AzureServiceBusReceiveEndpointTopology(
            topology.Name,
            topology.Durable,
            topology.Temporary,
            topology.PrefetchCount,
            topology.Bindings.ToArray());
    }
}
