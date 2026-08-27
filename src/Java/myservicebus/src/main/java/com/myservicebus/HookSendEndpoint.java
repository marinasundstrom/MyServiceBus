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
        Object body = message instanceof SendContext context ? context.getMessage() : message;
        boolean publish = message instanceof PublishContext;
        CompletableFuture<Void> operation = message instanceof SendContext context
                ? inner.send(context)
                : inner.send(message, cancellationToken);
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
                    null,
                    null));
        });
    }

    private static Throwable unwrap(Throwable throwable) {
        if (throwable instanceof CompletionException && throwable.getCause() != null) {
            return throwable.getCause();
        }
        return throwable;
    }
}
