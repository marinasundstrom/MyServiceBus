package com.myservicebus;

import java.net.URI;

import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;

class SendEndpointProviderImpl implements SendEndpointProvider {
    private final ConsumeContextProvider contextProvider;
    private final TransportSendEndpointProvider transportProvider;
    private final Logger logger;

    SendEndpointProviderImpl(ConsumeContextProvider contextProvider,
            TransportSendEndpointProvider transportProvider) {
        this(contextProvider, transportProvider, null);
    }

    SendEndpointProviderImpl(ConsumeContextProvider contextProvider,
            TransportSendEndpointProvider transportProvider,
            LoggerFactory loggerFactory) {
        this.contextProvider = contextProvider;
        this.transportProvider = transportProvider;
        this.logger = loggerFactory != null ? loggerFactory.create(SendEndpointProviderImpl.class) : null;
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        ConsumeContext<?> ctx = contextProvider.getContext();
        if (ctx != null) {
            return ctx.getSendEndpoint(uri);
        }

        SendEndpoint endpoint = transportProvider.getSendEndpoint(uri);
        return new LoggingSendEndpoint(endpoint, URI.create(uri), logger);
    }
}
