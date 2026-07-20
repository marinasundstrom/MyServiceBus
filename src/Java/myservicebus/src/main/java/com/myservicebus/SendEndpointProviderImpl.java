package com.myservicebus;

import java.net.URI;

import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;

class SendEndpointProviderImpl implements SendEndpointProvider {
    private final ConsumeContext<?> consumeContext;
    private final TransportSendEndpointProvider transportProvider;
    private final Logger logger;

    SendEndpointProviderImpl(ConsumeContextProvider contextProvider,
            TransportSendEndpointProvider transportProvider) {
        this(contextProvider, transportProvider, null);
    }

    SendEndpointProviderImpl(ConsumeContextProvider contextProvider,
            TransportSendEndpointProvider transportProvider,
            LoggerFactory loggerFactory) {
        this.consumeContext = contextProvider.getContext();
        this.transportProvider = transportProvider;
        this.logger = loggerFactory != null ? loggerFactory.create(SendEndpointProviderImpl.class) : null;
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        if (consumeContext != null) {
            return consumeContext.getSendEndpoint(uri);
        }

        SendEndpoint endpoint = transportProvider.getSendEndpoint(uri);
        return new LoggingSendEndpoint(endpoint, URI.create(uri), logger);
    }
}
