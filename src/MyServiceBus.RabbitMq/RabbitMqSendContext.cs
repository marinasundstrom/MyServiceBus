using System;
using System.Threading;
using MyServiceBus.Serialization;
using RabbitMQ.Client;

namespace MyServiceBus;

public class RabbitMqSendContext : SendContext
{
    public RabbitMqSendContext(Type[] messageTypes, IMessageSerializer serializer, CancellationToken cancellationToken = default)
        : base(messageTypes, serializer, cancellationToken)
    {
        Properties = new BasicProperties
        {
            Persistent = true
        };
    }

    public BasicProperties Properties { get; }
}

public class RabbitMqSendContextFactory : ISendContextFactory
{
    public SendContext Create(Type[] messageTypes, IMessageSerializer serializer, CancellationToken cancellationToken = default)
        => new RabbitMqSendContext(messageTypes, serializer, cancellationToken);
}

