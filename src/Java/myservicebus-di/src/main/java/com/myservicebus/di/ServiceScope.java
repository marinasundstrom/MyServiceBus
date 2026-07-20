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
    private Map<Key<?>, Object> instances;
    private boolean detached;
    private boolean closed;

    public ServiceScope(Injector injector, PerMessageScope scope) {
        this.injector = injector;
        this.scope = scope;
        this.scope.enter();
    }

    public ServiceProvider getServiceProvider() {
        return new ServiceProviderImpl(injector, scope);
    }

    /**
     * Ends the ambient resolution scope on the calling thread while retaining its
     * instances until this scope is closed. This allows asynchronous operations to
     * own scoped services until their completion without leaking ThreadLocal state.
     */
    public void detach() {
        if (!detached) {
            instances = scope.exit();
            detached = true;
        }
    }

    @Override
    public void close() {
        if (!closed) {
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
