using MyServiceBus.Topology;

namespace MyServiceBus;

public sealed record RabbitMqMessageBindingTopology(
    string ExchangeName,
    string ExchangeType,
    string RoutingKey);

public sealed record RabbitMqReceiveEndpointTopology(
    string QueueName,
    IReadOnlyList<RabbitMqMessageBindingTopology> Bindings,
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
            [new RabbitMqMessageBindingTopology(endpoint.ExchangeName, endpoint.ExchangeType, endpoint.RoutingKey)],
            endpoint.Durable,
            endpoint.AutoDelete,
            endpoint.PrefetchCount,
            endpoint.QueueArguments);
    }

    public static RabbitMqReceiveEndpointTopology Project(ReceiveEndpointTransportTopology endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var bindings = endpoint.Bindings
            .Select(binding =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(binding.EntityName);
                return new RabbitMqMessageBindingTopology(binding.EntityName, "fanout", string.Empty);
            })
            .ToArray();

        return new RabbitMqReceiveEndpointTopology(
            endpoint.Name,
            bindings,
            endpoint.Durable,
            endpoint.Temporary,
            endpoint.PrefetchCount,
            endpoint.TransportOptions is null
                ? null
                : new Dictionary<string, object?>(endpoint.TransportOptions, StringComparer.Ordinal));
    }
}
