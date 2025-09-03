package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;

import com.myservicebus.tasks.CancellationToken;

public class ErrorTransportFilter<T> implements Filter<ConsumeContext<T>> {
    @Override
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
        return next.send(context).handle((v, ex) -> {
            if (ex != null) {
                Throwable cause = ex instanceof CompletionException && ex.getCause() != null ? ex.getCause() : ex;
                String faultAddress = context.getFaultAddress();
                if (faultAddress != null) {
                    SendEndpoint endpoint = context.getSendEndpoint(faultAddress);
                    endpoint.send(context.getMessage(), CancellationToken.none).join();
                }
                throw new CompletionException(cause);
            }
            return null;
        });
    }
}
