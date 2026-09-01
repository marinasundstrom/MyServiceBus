package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;

final class BusHookConsumeFilter<T> implements Filter<ConsumeContext<T>> {
    private final BusHookDispatcher dispatcher;
    private final String endpointName;
    private final Class<?> messageType;

    BusHookConsumeFilter(BusHookDispatcher dispatcher, String endpointName, Class<?> messageType) {
        this.dispatcher = dispatcher;
        this.endpointName = endpointName;
        this.messageType = messageType;
    }

    @Override
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
        if (!dispatcher.isEnabled()) {
            return next.send(context);
        }
        long startedAt = System.nanoTime();
        return next.send(context).whenComplete((ignored, throwable) -> {
            Throwable failure = unwrap(throwable);
            dispatcher.dispatch(MessageOperationHookEvent.create(
                    failure == null ? "consumed" : "consume_faulted",
                    failure == null,
                    messageType,
                    endpointName,
                    null,
                    startedAt,
                    failure,
                    context.getCorrelationId() == null ? null : context.getCorrelationId().toString(),
                    context.getConversationId() == null ? null : context.getConversationId().toString(),
                    null,
                    null,
                    context.getMessageId() == null ? null : context.getMessageId().toString(),
                    null,
                    context.getRequestId() == null ? null : context.getRequestId().toString(),
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
