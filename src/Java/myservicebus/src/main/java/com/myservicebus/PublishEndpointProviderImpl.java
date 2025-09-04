package com.myservicebus;

public class PublishEndpointProviderImpl implements PublishEndpointProvider {
    private final ConsumeContextProvider contextProvider;
    private final MessageBus bus;

    public PublishEndpointProviderImpl(ConsumeContextProvider contextProvider, MessageBus bus) {
        this.contextProvider = contextProvider;
        this.bus = bus;
    }

    @Override
    public PublishEndpoint getPublishEndpoint() {
        ConsumeContext<?> ctx = contextProvider.getContext();
        if (ctx != null) {
            return ctx;
        }
        return bus;
    }
}
