package com.myservicebus;

import java.net.URI;
import java.util.Set;

import com.myservicebus.logging.LoggerFactory;

/**
 * Factory for creating {@link GenericRequestClient} instances.
 */
public class RequestClientFactory implements ScopedClientFactory {
    private final RequestClientTransport transport;
    private final BusHookDispatcher hooks;

    public RequestClientFactory(RequestClientTransport transport) {
        this(transport, Set.of(), null);
    }

    public RequestClientFactory(RequestClientTransport transport, Set<BusHook> hooks, LoggerFactory loggerFactory) {
        this.transport = transport;
        this.hooks = new BusHookDispatcher(hooks, loggerFactory);
    }

    @Override
    public <TRequest> RequestClient<TRequest> create(Class<TRequest> requestType, URI destinationAddress, RequestTimeout timeout) {
        return new GenericRequestClient<>(requestType, transport, destinationAddress, timeout, hooks);
    }
}
