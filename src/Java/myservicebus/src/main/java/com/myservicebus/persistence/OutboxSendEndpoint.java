package com.myservicebus.persistence;

import com.myservicebus.MessageUrn;
import com.myservicebus.SendContext;
import com.myservicebus.SendContextFactory;
import com.myservicebus.SendEndpoint;
import com.myservicebus.SendPipe;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import java.net.URI;
import java.util.concurrent.CompletableFuture;

public final class OutboxSendEndpoint implements SendEndpoint {
    private final OutboxSession session;
    private final SendEndpoint fallback;
    private final SendPipe sendPipe;
    private final MessageSerializer serializer;
    private final URI destination;
    private final URI source;
    private final SendContextFactory contextFactory;
    private final Runnable ensureStarted;

    public OutboxSendEndpoint(
            OutboxSession session,
            SendEndpoint fallback,
            SendPipe sendPipe,
            MessageSerializer serializer,
            URI destination,
            URI source,
            SendContextFactory contextFactory,
            Runnable ensureStarted) {
        this.session = session;
        this.fallback = fallback;
        this.sendPipe = sendPipe;
        this.serializer = serializer;
        this.destination = destination;
        this.source = source;
        this.contextFactory = contextFactory;
        this.ensureStarted = ensureStarted;
    }

    @Override
    public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
        SendContext context = contextFactory.create(message, cancellationToken);
        return capture(context);
    }

    @Override
    public CompletableFuture<Void> send(SendContext context) {
        return capture(context);
    }

    private CompletableFuture<Void> capture(SendContext context) {
        try {
            OutboxWriter writer = session.getWriter();
            if (writer == null) {
                return fallback.send(context);
            }
            ensureStarted.run();
            context.setSourceAddress(source);
            context.setDestinationAddress(destination);
            context.setMessageTypes(MessageUrn.forMessageTypes(context.getMessage().getClass()));
            return sendPipe.send(context).thenCompose(ignored -> {
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
