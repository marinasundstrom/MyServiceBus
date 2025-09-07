using System;
using System.Threading;
using MyServiceBus.Serialization;
using RabbitMQ.Client;

namespace MyServiceBus;

public class RabbitMqPublishContext : PublishContext
{
    public RabbitMqPublishContext(Type[] messageTypes, IMessageSerializer serializer, CancellationToken cancellationToken = default)
        : base(messageTypes, serializer, cancellationToken)
    {
        Properties = new BasicProperties
        {
            Persistent = true
        };
    }

    public BasicProperties Properties { get; }
}

public class RabbitMqPublishContextFactory : IPublishContextFactory
{
    public PublishContext Create(Type[] messageTypes, IMessageSerializer serializer, CancellationToken cancellationToken = default)
        => new RabbitMqPublishContext(messageTypes, serializer, cancellationToken);
}

