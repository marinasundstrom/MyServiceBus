using System;
using System.Threading;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public class SendContextFactory : ISendContextFactory
{
    public virtual SendContext Create(Type[] messageTypes, IMessageSerializer serializer, CancellationToken cancellationToken = default)
        => new(messageTypes, serializer, cancellationToken);
}

