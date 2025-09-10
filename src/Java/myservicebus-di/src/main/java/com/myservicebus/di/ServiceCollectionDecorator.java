package com.myservicebus.di;

import java.util.Iterator;
import java.util.List;

public abstract class ServiceCollectionDecorator implements ServiceCollection {
    protected final ServiceCollection inner;

    protected ServiceCollectionDecorator(ServiceCollection inner) {
        this.inner = inner;
    }

    @Override
    public <T extends ServiceCollection> T from(Class<T> decoratorType) {
        return inner.from(decoratorType);
    }

    @Override
    public Iterator<ServiceDescriptor> iterator() {
        return inner.iterator();
    }

    @Override
    public void add(ServiceDescriptor descriptor) {
        inner.add(descriptor);
    }

    @Override
    public <T> void addSingleton(Class<T> type, ServiceProviderBasedProvider<T> providerFactory) {
        inner.addSingleton(type, providerFactory);
    }

    @Override
    public <T, U extends T> void addSingleton(Class<T> type) {
        inner.addSingleton(type);
    }

    @Override
    public <T, U extends T> void addSingleton(Class<T> iface, Class<U> impl) {
        inner.addSingleton(iface, impl);
    }

    @Override
    public <T> void addScoped(Class<T> type) {
        inner.addScoped(type);
    }

    @Override
    public <T> void addScoped(Class<T> type, ServiceProviderBasedProvider<T> providerFactory) {
        inner.addScoped(type, providerFactory);
    }

    @Override
    public <T, U extends T> void addScoped(Class<T> iface, Class<U> impl) {
        inner.addScoped(iface, impl);
    }

    @Override
    public <T> boolean tryAddSingleton(Class<T> type, ServiceProviderBasedProvider<T> providerFactory) {
        return inner.tryAddSingleton(type, providerFactory);
    }

    @Override
    public <T, U extends T> boolean tryAddSingleton(Class<T> type) {
        return inner.tryAddSingleton(type);
    }

    @Override
    public <T, U extends T> boolean tryAddSingleton(Class<T> iface, Class<U> impl) {
        return inner.tryAddSingleton(iface, impl);
    }

    @Override
    public <T> boolean tryAddScoped(Class<T> type) {
        return inner.tryAddScoped(type);
    }

    @Override
    public <T> boolean tryAddScoped(Class<T> type, ServiceProviderBasedProvider<T> providerFactory) {
        return inner.tryAddScoped(type, providerFactory);
    }

    @Override
    public <T, U extends T> boolean tryAddScoped(Class<T> iface, Class<U> impl) {
        return inner.tryAddScoped(iface, impl);
    }

    @Override
    public <T, U extends T> void addMultiBinding(Class<T> iface, Class<U> impl) {
        inner.addMultiBinding(iface, impl);
    }

    @Override
    public <T, U extends T> void addScopedMultiBinding(Class<T> iface, Class<U> impl) {
        inner.addScopedMultiBinding(iface, impl);
    }

    @Override
    public <T> void remove(Class<T> type) {
        inner.remove(type);
    }

    @Override
    public boolean remove(ServiceDescriptor descriptor) {
        return inner.remove(descriptor);
    }

    @Override
    public List<ServiceDescriptor> getDescriptors() {
        return inner.getDescriptors();
    }

    @Override
    public ServiceProvider buildServiceProvider() {
        return inner.buildServiceProvider();
    }
}
