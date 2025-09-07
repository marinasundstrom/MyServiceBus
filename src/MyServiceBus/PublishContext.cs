using System;
using System.Threading;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public class PublishContext : SendContext, IPublishContext
{
    public PublishContext(Type[] messageTypes, IMessageSerializer messageSerializer, CancellationToken cancellationToken = default)
        : base(messageTypes, messageSerializer, cancellationToken)
    {
    }
}

