package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;

import com.myservicebus.tasks.CancellationToken;

public interface RequestClient<TRequest> {
        <TResponse> CompletableFuture<TResponse> getResponse(SendContext context, Class<TResponse> responseType);

        <T1, T2> CompletableFuture<Response2<T1, T2>> getResponse(SendContext context, Class<T1> responseType1,
                        Class<T2> responseType2);

        default <TResponse> CompletableFuture<TResponse> getResponse(TRequest request, Class<TResponse> responseType,
                        CancellationToken cancellationToken) {
                return getResponse(new SendContext(request, cancellationToken), responseType);
        }

        default <TResponse> CompletableFuture<TResponse> getResponse(TRequest request, Class<TResponse> responseType,
                        Consumer<SendContext> contextCallback, CancellationToken cancellationToken) {
                SendContext ctx = new SendContext(request, cancellationToken);
                contextCallback.accept(ctx);
                return getResponse(ctx, responseType);
        }

        default <TResponse> CompletableFuture<TResponse> getResponse(TRequest request, Class<TResponse> responseType,
                        Consumer<SendContext> contextCallback) {
                return getResponse(request, responseType, contextCallback, CancellationToken.none);
        }

        default <TResponse> CompletableFuture<TResponse> getResponse(TRequest request, Class<TResponse> responseType)
                        {
                return getResponse(request, responseType, CancellationToken.none);
        }

        default <T1, T2> CompletableFuture<Response2<T1, T2>> getResponse(TRequest request, Class<T1> responseType1,
                        Class<T2> responseType2, CancellationToken cancellationToken) {
                return getResponse(new SendContext(request, cancellationToken), responseType1, responseType2);
        }

        default <T1, T2> CompletableFuture<Response2<T1, T2>> getResponse(TRequest request, Class<T1> responseType1,
                        Class<T2> responseType2, Consumer<SendContext> contextCallback,
                        CancellationToken cancellationToken) {
                SendContext ctx = new SendContext(request, cancellationToken);
                contextCallback.accept(ctx);
                return getResponse(ctx, responseType1, responseType2);
        }

        default <T1, T2> CompletableFuture<Response2<T1, T2>> getResponse(TRequest request, Class<T1> responseType1,
                        Class<T2> responseType2, Consumer<SendContext> contextCallback) {
                return getResponse(request, responseType1, responseType2, contextCallback, CancellationToken.none);
        }

        default <T1, T2> CompletableFuture<Response2<T1, T2>> getResponse(TRequest request, Class<T1> responseType1,
                        Class<T2> responseType2) {
                return getResponse(request, responseType1, responseType2, CancellationToken.none);
        }
}