package com.myservicebus.di;

import java.util.Set;

import com.google.inject.ConfigurationException;
import com.google.inject.Injector;
import com.google.inject.Key;
import com.google.inject.TypeLiteral;
import com.google.inject.util.Types;

final class ServiceProviderImpl implements ServiceProvider {
    private Injector root;
    private final PerMessageScope scope;
    private final ServiceScope owner;

    ServiceProviderImpl(Injector root, PerMessageScope scope) {
        this(root, scope, null);
    }

    ServiceProviderImpl(Injector root, PerMessageScope scope, ServiceScope owner) {
        this.root = root;
        this.scope = scope;
        this.owner = owner;
    }

    public <T> T getService(Class<T> type) {
        try {
            return resolve(() -> root.getInstance(type));
        } catch (ConfigurationException ex) {
            return null;
        }
    }

    @SuppressWarnings("unchecked")
    public <T> Set<T> getServices(Class<T> iface) {
        TypeLiteral<Set<T>> setType = (TypeLiteral<Set<T>>) TypeLiteral.get(Types.setOf(iface));
        try {
            return resolve(() -> root.getInstance(Key.get(setType)));
        } catch (ConfigurationException ex) {
            return java.util.Collections.emptySet();
        }
    }

    public ServiceScope createScope() {
        return new ServiceScope(root, scope);
    }

    void setInjector(Injector injector) {
        root = injector;
    }

    private <T> T resolve(java.util.function.Supplier<T> resolve) {
        return owner == null ? resolve.get() : owner.resolve(resolve);
    }
}
