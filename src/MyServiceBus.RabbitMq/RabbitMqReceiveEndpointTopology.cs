using MyServiceBus.Topology;

namespace MyServiceBus;

public sealed record RabbitMqReceiveEndpointTopology(
    string QueueName,
    string ExchangeName,
    string RoutingKey,
    string ExchangeType,
    bool Durable,
    bool AutoDelete,
    ushort PrefetchCount,
    IDictionary<string, object?>? QueueArguments)
{
    public static RabbitMqReceiveEndpointTopology Project(ReceiveEndpointTopology endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint.QueueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint.ExchangeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint.ExchangeType);

        if (endpoint.Durable && endpoint.AutoDelete)
            throw new ArgumentException("A RabbitMQ receive endpoint cannot be both durable and auto-delete.", nameof(endpoint));

        return new RabbitMqReceiveEndpointTopology(
            endpoint.QueueName,
            endpoint.ExchangeName,
            endpoint.RoutingKey,
            endpoint.ExchangeType,
            endpoint.Durable,
            endpoint.AutoDelete,
            endpoint.PrefetchCount,
            endpoint.QueueArguments);
    }
}
