using System.Collections.Generic;

namespace MyServiceBus;

public class RabbitMqEndpointSettings
{
    public string QueueName { get; init; } = default!;
    public string ExchangeName { get; init; } = default!;
    public string RoutingKey { get; init; } = string.Empty;
    public string ExchangeType { get; init; } = "fanout";
    public bool Durable { get; init; } = true;
    public bool AutoDelete { get; init; } = false;
    public IDictionary<string, object?>? QueueArguments { get; init; }
}
