package com.myservicebus;

import com.myservicebus.persistence.OutboxPublishEndpoint;
import com.myservicebus.persistence.OutboxSession;
import com.myservicebus.serialization.MessageSerializer;

class PublishEndpointProviderImpl implements PublishEndpointProvider {
    private final ConsumeContextProvider contextProvider;
    private final MessageBus bus;
    private final OutboxSession outboxSession;
    private final TransportFactory transportFactory;
    private final SendPipe sendPipe;
    private final PublishPipe publishPipe;
    private final MessageSerializer serializer;
    private final PublishContextFactory contextFactory;

    PublishEndpointProviderImpl(ConsumeContextProvider contextProvider, MessageBus bus) {
        this(contextProvider, bus, null, null, null, null, null, null);
    }

    PublishEndpointProviderImpl(
            ConsumeContextProvider contextProvider,
            MessageBus bus,
            OutboxSession outboxSession,
            TransportFactory transportFactory,
            SendPipe sendPipe,
            PublishPipe publishPipe,
            MessageSerializer serializer,
            PublishContextFactory contextFactory) {
        this.contextProvider = contextProvider;
        this.bus = bus;
        this.outboxSession = outboxSession;
        this.transportFactory = transportFactory;
        this.sendPipe = sendPipe;
        this.publishPipe = publishPipe;
        this.serializer = serializer;
        this.contextFactory = contextFactory;
    }

    @Override
    public PublishEndpoint getPublishEndpoint() {
        ConsumeContext<?> ctx = contextProvider.getContext();
        if ((outboxSession == null || outboxSession.getWriter() == null) && ctx != null) {
            return ctx;
        }
        if (outboxSession != null) {
            return new OutboxPublishEndpoint(
                    outboxSession,
                    bus,
                    transportFactory,
                    sendPipe,
                    publishPipe,
                    serializer,
                    bus,
                    contextFactory != null ? contextFactory : new DefaultPublishContextFactory(),
                    this::ensureStarted);
        }
        return bus;
    }

    private void ensureStarted() {
        if (bus instanceof MessageBusImpl hostedBus && !hostedBus.isStarted()) {
            throw new IllegalStateException("The service bus is not started.");
        }
    }
}
