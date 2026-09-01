package com.myservicebus;

import java.net.URI;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;

import com.myservicebus.tasks.CancellationToken;

final class HookSendEndpoint implements SendEndpoint {
    private final SendEndpoint inner;
    private final URI destinationAddress;
    private final BusHookDispatcher dispatcher;
    private final boolean observePublish;

    HookSendEndpoint(SendEndpoint inner, URI destinationAddress, BusHookDispatcher dispatcher) {
        this(inner, destinationAddress, dispatcher, false);
    }

    HookSendEndpoint(SendEndpoint inner, URI destinationAddress, BusHookDispatcher dispatcher, boolean observePublish) {
        this.inner = inner;
        this.destinationAddress = destinationAddress;
        this.dispatcher = dispatcher;
        this.observePublish = observePublish;
    }

    @Override
    public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
        long startedAt = System.nanoTime();
        SendContext sendContext = message instanceof SendContext context ? context : null;
        Object body = sendContext != null ? sendContext.getMessage() : message;
        CompletableFuture<Void> operation = sendContext != null
                ? inner.send(sendContext)
                : inner.send(message, cancellationToken);
        return observe(body, sendContext, operation, startedAt);
    }

    @Override
    public CompletableFuture<Void> send(SendContext context) {
        long startedAt = System.nanoTime();
        return observe(context.getMessage(), context, inner.send(context), startedAt);
    }

    private CompletableFuture<Void> observe(
            Object body,
            SendContext sendContext,
            CompletableFuture<Void> operation,
            long startedAt) {
        boolean publish = sendContext instanceof PublishContext;
        return operation.whenComplete((ignored, throwable) -> {
            if (publish && !observePublish) {
                return;
            }
            Throwable failure = unwrap(throwable);
            dispatcher.dispatch(MessageOperationHookEvent.create(
                    publish
                            ? (failure == null ? "published" : "publish_faulted")
                            : (failure == null ? "sent" : "send_faulted"),
                    failure == null,
                    body.getClass(),
                    null,
                    destinationAddress.toString(),
                    startedAt,
                    failure,
                    sendContext == null || sendContext.getCorrelationId() == null
                            ? null
                            : sendContext.getCorrelationId().toString(),
                    sendContext == null || sendContext.getConversationId() == null
                            ? null
                            : sendContext.getConversationId().toString(),
                    null,
                    null,
                    sendContext == null || sendContext.getMessageId() == null
                            ? null
                            : sendContext.getMessageId().toString(),
                    sendContext == null || sendContext.getCausationMessageId() == null
                            ? null
                            : sendContext.getCausationMessageId().toString()));
        });
    }

    private static Throwable unwrap(Throwable throwable) {
        if (throwable instanceof CompletionException && throwable.getCause() != null) {
            return throwable.getCause();
        }
        return throwable;
    }
}
