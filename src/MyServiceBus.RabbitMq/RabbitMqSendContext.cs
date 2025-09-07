using System;
using System.Threading;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public class RabbitMqSendContext : SendContext, IQueueSendContext
{
    public RabbitMqSendContext(Type[] messageTypes, IMessageSerializer serializer, CancellationToken cancellationToken = default)
        : base(messageTypes, serializer, cancellationToken)
    {
    }

    public string? RoutingKey { get; set; }
}

