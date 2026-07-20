package com.myservicebus;

import java.net.URI;
import java.util.concurrent.CompletableFuture;

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
        return transport.sendRequest(requestType, context, responseType);
    }

    @Override
    public <T1, T2> CompletableFuture<Response2<T1, T2>> getResponse(SendContext context, Class<T1> responseType1,
            Class<T2> responseType2) {
        applyDestination(context);
        return transport.sendRequest(requestType, context, responseType1, responseType2);
    }

    private void applyDestination(SendContext context) {
        if (destinationAddress != null) {
            context.setDestinationAddress(destinationAddress);
        }
    }
}
