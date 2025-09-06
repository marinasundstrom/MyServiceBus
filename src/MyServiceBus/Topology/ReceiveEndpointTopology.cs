namespace MyServiceBus.Topology;

public class ReceiveEndpointTopology
{
    public string QueueName { get; init; } = default!;
    public string ExchangeName { get; init; } = default!;
    public string RoutingKey { get; init; } = default!;
    public string ExchangeType { get; init; } = "fanout";
    public bool Durable { get; init; } = true;
    public bool AutoDelete { get; init; } = false;
    public ushort PrefetchCount { get; init; } = 0;
}