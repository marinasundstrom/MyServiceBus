using System;
using System.Collections.Generic;
using System.Threading;
using MyServiceBus.Serialization;
using RabbitMQ.Client;

namespace MyServiceBus;

public class RabbitMqReceiveContext : ReceiveContextImpl, IQueueReceiveContext
{
    public RabbitMqReceiveContext(IMessageContext messageContext, IReadOnlyBasicProperties properties, ulong deliveryTag, string exchange, string routingKey, Uri? errorAddress = null, CancellationToken cancellationToken = default)
        : base(messageContext, errorAddress, cancellationToken)
    {
        Properties = properties;
        DeliveryTag = deliveryTag;
        Exchange = exchange;
        RoutingKey = routingKey;
        BrokerProperties = properties.Headers != null
            ? new Dictionary<string, object>(properties.Headers)
            : new Dictionary<string, object>();
    }

    public IReadOnlyBasicProperties Properties { get; }
    public ulong DeliveryTag { get; }
    public string Exchange { get; }
    public string RoutingKey { get; }

    public long DeliveryCount => (long)DeliveryTag;
    public string? Destination => Exchange;
    public IDictionary<string, object> BrokerProperties { get; }
}

