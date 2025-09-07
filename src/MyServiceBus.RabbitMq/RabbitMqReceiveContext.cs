using System;
using System.Threading;
using MyServiceBus.Serialization;
using RabbitMQ.Client;

namespace MyServiceBus;

public class RabbitMqReceiveContext : ReceiveContextImpl
{
    public RabbitMqReceiveContext(IMessageContext messageContext, IReadOnlyBasicProperties properties, ulong deliveryTag, string exchange, string routingKey, Uri? errorAddress = null, CancellationToken cancellationToken = default)
        : base(messageContext, errorAddress, cancellationToken)
    {
        Properties = properties;
        DeliveryTag = deliveryTag;
        Exchange = exchange;
        RoutingKey = routingKey;
    }

    public IReadOnlyBasicProperties Properties { get; }
    public ulong DeliveryTag { get; }
    public string Exchange { get; }
    public string RoutingKey { get; }
}

