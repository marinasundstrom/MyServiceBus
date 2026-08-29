package com.myservicebus.persistence;

import com.myservicebus.MessageBus;
import com.myservicebus.MessageUrn;
import com.myservicebus.PublishContext;
import com.myservicebus.PublishContextFactory;
import com.myservicebus.PublishEndpoint;
import com.myservicebus.PublishPipe;
import com.myservicebus.SendPipe;
import com.myservicebus.TransportFactory;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import java.net.URI;
import java.util.concurrent.CompletableFuture;

public final class OutboxPublishEndpoint implements PublishEndpoint {
    private final OutboxSession session;
    private final PublishEndpoint fallback;
    private final TransportFactory transportFactory;
    private final SendPipe sendPipe;
    private final PublishPipe publishPipe;
    private final MessageSerializer serializer;
    private final MessageBus bus;
    private final PublishContextFactory contextFactory;
    private final Runnable ensureStarted;

    public OutboxPublishEndpoint(
            OutboxSession session,
            PublishEndpoint fallback,
            TransportFactory transportFactory,
            SendPipe sendPipe,
            PublishPipe publishPipe,
            MessageSerializer serializer,
            MessageBus bus,
            PublishContextFactory contextFactory,
            Runnable ensureStarted) {
        this.session = session;
        this.fallback = fallback;
        this.transportFactory = transportFactory;
        this.sendPipe = sendPipe;
        this.publishPipe = publishPipe;
        this.serializer = serializer;
        this.bus = bus;
        this.contextFactory = contextFactory;
        this.ensureStarted = ensureStarted;
    }

    @Override
    public <T> CompletableFuture<Void> publish(T message, CancellationToken cancellationToken) {
        return capture(contextFactory.create(message, cancellationToken));
    }

    @Override
    public CompletableFuture<Void> publish(PublishContext context) {
        return capture(context);
    }

    private CompletableFuture<Void> capture(PublishContext context) {
        try {
            OutboxWriter writer = session.getWriter();
            if (writer == null) {
                return fallback.publish(context);
            }
            ensureStarted.run();
            Class<?> messageType = context.getMessage().getClass();
            context.setSourceAddress(bus.getAddress());
            context.setDestinationAddress(URI.create(transportFactory.getPublishAddress(messageType)));
            context.setMessageTypes(MessageUrn.forMessageTypes(messageType));
            return publishPipe.send(context)
                    .thenCompose(ignored -> sendPipe.send(context))
                    .thenCompose(ignored -> {
                        try {
                            return writer.add(
                                    OutboxMessageFactory.create(context, serializer),
                                    context.getCancellationToken());
                        } catch (Exception failure) {
                            return CompletableFuture.failedFuture(failure);
                        }
                    });
        } catch (Exception failure) {
            return CompletableFuture.failedFuture(failure);
        }
    }
}
