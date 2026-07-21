package com.myservicebus;

import java.net.URI;
import java.time.Duration;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;
import java.util.UUID;

import com.myservicebus.tasks.CancellationRegistration;
import com.myservicebus.tasks.CancellationToken;

/**
 * Generic request client that delegates to a transport-specific implementation.
 */
public class GenericRequestClient<TRequest> implements RequestClient<TRequest> {
    private final Class<TRequest> requestType;
    private final RequestClientTransport transport;
    private final URI destinationAddress;
    private final RequestTimeout timeout;

    public GenericRequestClient(Class<TRequest> requestType, RequestClientTransport transport) {
        this(requestType, transport, null, RequestTimeout.DEFAULT);
    }

    public GenericRequestClient(Class<TRequest> requestType, RequestClientTransport transport, URI destinationAddress,
            RequestTimeout timeout) {
        this.requestType = requestType;
        this.transport = transport;
        this.destinationAddress = destinationAddress;
        this.timeout = timeout == null ? RequestTimeout.DEFAULT : timeout;
    }

    @Override
    public <TResponse> CompletableFuture<TResponse> getResponse(SendContext context, Class<TResponse> responseType) {
        applyDestination(context);
        if (context.getCancellationToken().isCancelled()) {
            return cancelledFuture();
        }
        return applyRequestPolicies(transport.sendRequest(requestType, context, responseType), context);
    }

    @Override
    public <T1, T2> CompletableFuture<Response2<T1, T2>> getResponse(SendContext context, Class<T1> responseType1,
            Class<T2> responseType2) {
        applyDestination(context);
        if (context.getCancellationToken().isCancelled()) {
            return cancelledFuture();
        }
        return applyRequestPolicies(transport.sendRequest(requestType, context, responseType1, responseType2), context);
    }

    private void applyDestination(SendContext context) {
        if (context.getRequestId() == null) {
            context.setRequestId(UUID.randomUUID());
        }
        if (destinationAddress != null) {
            context.setDestinationAddress(destinationAddress);
        }
    }

    private <T> CompletableFuture<T> applyRequestPolicies(CompletableFuture<T> response, SendContext context) {
        CancellationToken cancellationToken = context.getCancellationToken();
        CancellationRegistration registration = cancellationToken.onCancel(() -> response.cancel(false));

        Duration duration = timeout.getDuration();
        if (!duration.isZero() && !duration.isNegative()) {
            response.orTimeout(duration.toMillis(), TimeUnit.MILLISECONDS);
        }

        response.whenComplete((result, exception) -> registration.close());
        return response;
    }

    private static <T> CompletableFuture<T> cancelledFuture() {
        CompletableFuture<T> future = new CompletableFuture<>();
        future.cancel(false);
        return future;
    }
}
