using System;
using System.Threading;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public class PublishContextFactory : IPublishContextFactory
{
    public PublishContext Create(Type[] messageTypes, IMessageSerializer serializer, CancellationToken cancellationToken = default)
        => new PublishContext(messageTypes, serializer, cancellationToken);
}

