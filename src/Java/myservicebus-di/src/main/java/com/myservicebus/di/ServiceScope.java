package com.myservicebus.di;

import com.google.inject.Injector;
import com.google.inject.Key;
import java.io.Closeable;
import java.util.Collections;
import java.util.IdentityHashMap;
import java.util.Map;
import java.util.Set;

public class ServiceScope implements Closeable {
    private final Injector injector;
    private final PerMessageScope scope;
    private final ServiceProvider directProvider;
    private final Runnable directClose;
    private Map<Key<?>, Object> instances;
    private boolean detached;
    private boolean closed;

    ServiceScope(Injector injector, PerMessageScope scope) {
        this.injector = injector;
        this.scope = scope;
        this.directProvider = null;
        this.directClose = null;
        this.scope.enter();
    }

    /**
     * Creates a scope owned by a custom service-provider implementation.
     *
     * @param directProvider provider exposed for this scope
     * @param directClose cleanup action invoked once when the scope closes
     */
    public ServiceScope(ServiceProvider directProvider, Runnable directClose) {
        if (directProvider == null) {
            throw new IllegalArgumentException("directProvider must not be null");
        }
        if (directClose == null) {
            throw new IllegalArgumentException("directClose must not be null");
        }
        this.injector = null;
        this.scope = null;
        this.directProvider = directProvider;
        this.directClose = directClose;
    }

    public ServiceProvider getServiceProvider() {
        if (directProvider != null) {
            return directProvider;
        }
        return new ServiceProviderImpl(injector, scope, this);
    }

    /**
     * Ends the ambient resolution scope on the calling thread while retaining its
     * instances until this scope is closed. This allows asynchronous operations to
     * own scoped services until their completion without leaking ThreadLocal state.
     */
    public void detach() {
        if (directProvider != null) {
            detached = true;
            return;
        }
        if (!detached) {
            instances = scope.exit();
            detached = true;
        }
    }

    <T> T resolve(java.util.function.Supplier<T> resolve) {
        if (!detached) {
            return resolve.get();
        }
        if (closed) {
            throw new IllegalStateException("The service scope is closed.");
        }
        synchronized (instances) {
            scope.enter(instances);
            try {
                return resolve.get();
            } finally {
                Map<Key<?>, Object> active = scope.exit();
                if (active != instances) {
                    throw new IllegalStateException("The detached service scope became unbalanced.");
                }
            }
        }
    }

    @Override
    public void close() {
        if (!closed) {
            if (directClose != null) {
                directClose.run();
                closed = true;
                return;
            }
            detach();
            Set<Object> disposed = Collections.newSetFromMap(new IdentityHashMap<>());
            for (Object instance : instances.values()) {
                if (instance instanceof AutoCloseable closeable && disposed.add(instance)) {
                    try {
                        closeable.close();
                    } catch (Exception ex) {
                        throw new RuntimeException("Failed to close scoped service " + instance.getClass().getName(), ex);
                    }
                }
            }
            closed = true;
        }
    }
}
