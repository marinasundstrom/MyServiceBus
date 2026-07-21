package com.myservicebus;

import java.net.URI;

import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;

class SendEndpointProviderImpl implements SendEndpointProvider {
    private final ConsumeContext<?> consumeContext;
    private final TransportSendEndpointProvider transportProvider;
    private final Logger logger;
    private final MessageBus messageBus;

    SendEndpointProviderImpl(ConsumeContextProvider contextProvider,
            TransportSendEndpointProvider transportProvider) {
        this(contextProvider, transportProvider, null, null);
    }

    SendEndpointProviderImpl(ConsumeContextProvider contextProvider,
            TransportSendEndpointProvider transportProvider,
            LoggerFactory loggerFactory) {
        this(contextProvider, transportProvider, loggerFactory, null);
    }

    SendEndpointProviderImpl(ConsumeContextProvider contextProvider,
            TransportSendEndpointProvider transportProvider,
            LoggerFactory loggerFactory,
            MessageBus messageBus) {
        this.consumeContext = contextProvider.getContext();
        this.transportProvider = transportProvider;
        this.logger = loggerFactory != null ? loggerFactory.create(SendEndpointProviderImpl.class) : null;
        this.messageBus = messageBus;
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        if (consumeContext != null) {
            return consumeContext.getSendEndpoint(uri);
        }

        SendEndpoint endpoint = transportProvider.getSendEndpoint(uri);
        SendEndpoint loggingEndpoint = new LoggingSendEndpoint(endpoint, URI.create(uri), logger);
        if (!(messageBus instanceof MessageBusImpl hostedBus)) {
            return loggingEndpoint;
        }

        return new SendEndpoint() {
            @Override
            public <T> java.util.concurrent.CompletableFuture<Void> send(
                    T message,
                    com.myservicebus.tasks.CancellationToken cancellationToken) {
                return hostedBus.isStarted()
                        ? loggingEndpoint.send(message, cancellationToken)
                        : MessageBusImpl.notStartedFuture();
            }
        };
    }
}
