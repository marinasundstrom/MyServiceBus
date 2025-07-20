package com.myservicebus.di;

import com.google.inject.Injector;
import java.io.Closeable;

public class ServiceScope implements Closeable {
    private final Injector injector;
    private final PerMessageScope scope;
    private boolean closed;

    public ServiceScope(Injector injector, PerMessageScope scope) {
        this.injector = injector;
        this.scope = scope;
        this.scope.enter();
    }

    public ServiceProvider getServiceProvider() {
        return new ServiceProviderImpl(injector, scope);
    }

    @Override
    public void close() {
        if (!closed) {
            scope.exit();
            closed = true;
        }
    }
}