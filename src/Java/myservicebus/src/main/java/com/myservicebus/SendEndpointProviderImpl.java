package com.myservicebus;

public class SendEndpointProviderImpl implements SendEndpointProvider {
    private final ConsumeContextProvider contextProvider;
    private final TransportSendEndpointProvider transportProvider;

    public SendEndpointProviderImpl(ConsumeContextProvider contextProvider,
            TransportSendEndpointProvider transportProvider) {
        this.contextProvider = contextProvider;
        this.transportProvider = transportProvider;
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        ConsumeContext<?> ctx = contextProvider.getContext();
        if (ctx != null) {
            return ctx.getSendEndpoint(uri);
        }
        return transportProvider.getSendEndpoint(uri);
    }
}
