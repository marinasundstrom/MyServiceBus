package com.myservicebus;

import java.net.URI;

import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.persistence.OutboxSendEndpoint;
import com.myservicebus.persistence.OutboxSession;
import com.myservicebus.serialization.MessageSerializer;

class SendEndpointProviderImpl implements SendEndpointProvider {
    private final ConsumeContext<?> consumeContext;
    private final TransportSendEndpointProvider transportProvider;
    private final Logger logger;
    private final MessageBus messageBus;
    private final OutboxSession outboxSession;
    private final SendPipe sendPipe;
    private final MessageSerializer serializer;
    private final SendContextFactory contextFactory;

    SendEndpointProviderImpl(ConsumeContextProvider contextProvider,
            TransportSendEndpointProvider transportProvider) {
        this(contextProvider, transportProvider, null, null, null, null, null, null);
    }

    SendEndpointProviderImpl(ConsumeContextProvider contextProvider,
            TransportSendEndpointProvider transportProvider,
            LoggerFactory loggerFactory) {
        this(contextProvider, transportProvider, loggerFactory, null, null, null, null, null);
    }

    SendEndpointProviderImpl(ConsumeContextProvider contextProvider,
            TransportSendEndpointProvider transportProvider,
            LoggerFactory loggerFactory,
            MessageBus messageBus) {
        this(contextProvider, transportProvider, loggerFactory, messageBus, null, null, null, null);
    }

    SendEndpointProviderImpl(
            ConsumeContextProvider contextProvider,
            TransportSendEndpointProvider transportProvider,
            LoggerFactory loggerFactory,
            MessageBus messageBus,
            OutboxSession outboxSession,
            SendPipe sendPipe,
            MessageSerializer serializer,
            SendContextFactory contextFactory) {
        this.consumeContext = contextProvider.getContext();
        this.transportProvider = transportProvider;
        this.logger = loggerFactory != null ? loggerFactory.create(SendEndpointProviderImpl.class) : null;
        this.messageBus = messageBus;
        this.outboxSession = outboxSession;
        this.sendPipe = sendPipe;
        this.serializer = serializer;
        this.contextFactory = contextFactory;
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        if ((outboxSession == null || outboxSession.getWriter() == null) && consumeContext != null) {
            return consumeContext.getSendEndpoint(uri);
        }

        SendEndpoint endpoint = transportProvider.getSendEndpoint(uri);
        SendEndpoint loggingEndpoint = new LoggingSendEndpoint(endpoint, URI.create(uri), logger);
        SendEndpoint fallback;
        if (!(messageBus instanceof MessageBusImpl hostedBus)) {
            fallback = loggingEndpoint;
        } else {
            fallback = new SendEndpoint() {
                @Override
                public <T> java.util.concurrent.CompletableFuture<Void> send(
                        T message,
                        com.myservicebus.tasks.CancellationToken cancellationToken) {
                    return hostedBus.isStarted()
                            ? loggingEndpoint.send(message, cancellationToken)
                            : MessageBusImpl.notStartedFuture();
                }

                @Override
                public java.util.concurrent.CompletableFuture<Void> send(SendContext context) {
                    return hostedBus.isStarted()
                            ? loggingEndpoint.send(context)
                            : MessageBusImpl.notStartedFuture();
                }
            };
        }

        if (outboxSession == null) {
            return fallback;
        }
        return new OutboxSendEndpoint(
                outboxSession,
                fallback,
                sendPipe,
                serializer,
                URI.create(uri),
                messageBus.getAddress(),
                contextFactory != null ? contextFactory : new DefaultSendContextFactory(),
                this::ensureStarted);
    }

    private void ensureStarted() {
        if (messageBus instanceof MessageBusImpl hostedBus && !hostedBus.isStarted()) {
            throw new IllegalStateException("The service bus is not started.");
        }
    }
}
