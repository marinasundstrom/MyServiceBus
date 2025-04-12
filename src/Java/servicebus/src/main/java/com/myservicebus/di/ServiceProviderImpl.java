package com.myservicebus.di;

import com.google.inject.Injector;

public class ServiceProviderImpl implements ServiceProvider {
    private Injector root;
    private final PerMessageScope scope;

    public ServiceProviderImpl(Injector root, PerMessageScope scope) {
        this.root = root;
        this.scope = scope;
    }

    public <T> T getService(Class<T> type) {
        return root.getInstance(type); // for singletons
    }

    public ServiceScope createScope() {
        return new ServiceScope(root, scope);
    }

    public void setInjector(Injector injector) {
        root = injector;
    }
}