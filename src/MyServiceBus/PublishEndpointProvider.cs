using System;
using MyServiceBus.Persistence;
using MyServiceBus.Serialization;

namespace MyServiceBus;

internal class PublishEndpointProvider : IPublishEndpointProvider
{
    readonly ConsumeContextProvider contextProvider;
    readonly IMessageBus bus;
    readonly OutboxSession? outboxSession;
    readonly ITransportFactory transportFactory;
    readonly ISendPipe sendPipe;
    readonly IPublishPipe publishPipe;
    readonly IMessageSerializer serializer;
    readonly IPublishContextFactory contextFactory;

    public PublishEndpointProvider(
        ConsumeContextProvider contextProvider,
        IMessageBus bus,
        ITransportFactory transportFactory,
        ISendPipe sendPipe,
        IPublishPipe publishPipe,
        IMessageSerializer serializer,
        IPublishContextFactory contextFactory,
        OutboxSession? outboxSession = null)
    {
        this.contextProvider = contextProvider;
        this.bus = bus;
        this.transportFactory = transportFactory;
        this.sendPipe = sendPipe;
        this.publishPipe = publishPipe;
        this.serializer = serializer;
        this.contextFactory = contextFactory;
        this.outboxSession = outboxSession;
    }

    public IPublishEndpoint GetPublishEndpoint()
    {
        var ctx = contextProvider.Context;
        if (ctx != null)
            return ctx;
        if (outboxSession is null)
            return (IPublishEndpoint)bus;

        Action? ensureStarted = bus is MessageBus messageBus ? messageBus.EnsureStarted : null;
        return new OutboxPublishEndpoint(
            outboxSession, (IPublishEndpoint)bus, transportFactory, sendPipe, publishPipe, serializer, bus.Address,
            contextFactory, ensureStarted);
    }
}
