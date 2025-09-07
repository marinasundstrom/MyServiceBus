using System;
using System.Threading;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public interface ISendContextFactory
{
    SendContext Create(Type[] messageTypes, IMessageSerializer serializer, CancellationToken cancellationToken = default);
}

