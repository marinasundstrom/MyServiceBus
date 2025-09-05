package com.myservicebus.di;

import java.util.Set;

import com.google.inject.ConfigurationException;
import com.google.inject.Injector;
import com.google.inject.Key;
import com.google.inject.TypeLiteral;
import com.google.inject.util.Types;

public class ServiceProviderImpl implements ServiceProvider {
    private Injector root;
    private final PerMessageScope scope;

    public ServiceProviderImpl(Injector root, PerMessageScope scope) {
        this.root = root;
        this.scope = scope;
    }

    public <T> T getService(Class<T> type) {
        try {
            return root.getInstance(type); // for singletons
        } catch (ConfigurationException ex) {
            return null;
        }
    }

    @SuppressWarnings("unchecked")
    public <T> Set<T> getServices(Class<T> iface) {
        TypeLiteral<Set<T>> setType = (TypeLiteral<Set<T>>) TypeLiteral.get(Types.setOf(iface));
        try {
            return root.getInstance(Key.get(setType));
        } catch (ConfigurationException ex) {
            return java.util.Collections.emptySet();
        }
    }

    public ServiceScope createScope() {
        return new ServiceScope(root, scope);
    }

    public void setInjector(Injector injector) {
        root = injector;
    }
}