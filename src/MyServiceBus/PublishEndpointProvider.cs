using System;

namespace MyServiceBus;

internal class PublishEndpointProvider : IPublishEndpointProvider
{
    readonly ConsumeContextProvider contextProvider;
    readonly IMessageBus bus;

    public PublishEndpointProvider(ConsumeContextProvider contextProvider, IMessageBus bus)
    {
        this.contextProvider = contextProvider;
        this.bus = bus;
    }

    public IPublishEndpoint GetPublishEndpoint()
    {
        var ctx = contextProvider.Context;
        return ctx != null ? ctx : (IPublishEndpoint)bus;
    }
}
