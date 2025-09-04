package com.myservicebus;

import java.net.URI;

public interface ScopedClientFactory {
    default <TRequest> RequestClient<TRequest> create(Class<TRequest> requestType) {
        return create(requestType, null, RequestTimeout.DEFAULT);
    }

    default <TRequest> RequestClient<TRequest> create(Class<TRequest> requestType, RequestTimeout timeout) {
        return create(requestType, null, timeout);
    }

    default <TRequest> RequestClient<TRequest> create(Class<TRequest> requestType, URI destinationAddress) {
        return create(requestType, destinationAddress, RequestTimeout.DEFAULT);
    }

    <TRequest> RequestClient<TRequest> create(Class<TRequest> requestType, URI destinationAddress, RequestTimeout timeout);
}
