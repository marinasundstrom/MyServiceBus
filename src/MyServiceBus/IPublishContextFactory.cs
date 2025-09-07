using System;
using System.Threading;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public interface IPublishContextFactory
{
    PublishContext Create(Type[] messageTypes, IMessageSerializer serializer, CancellationToken cancellationToken = default);
}

