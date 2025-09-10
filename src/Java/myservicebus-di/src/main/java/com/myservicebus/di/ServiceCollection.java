package com.myservicebus.di;

import java.util.List;

public interface ServiceCollection extends Iterable<ServiceDescriptor> {
    static ServiceCollection create() {
        return new DefaultServiceCollection();
    }

    <T extends ServiceCollection> T from(Class<T> decoratorType);

    void add(ServiceDescriptor descriptor);

    <T> void addSingleton(Class<T> type, ServiceProviderBasedProvider<T> providerFactory);

    <T, U extends T> void addSingleton(Class<T> type);

    <T, U extends T> void addSingleton(Class<T> iface, Class<U> impl);

    <T> void addScoped(Class<T> type);

    <T> void addScoped(Class<T> type, ServiceProviderBasedProvider<T> providerFactory);

    <T, U extends T> void addScoped(Class<T> iface, Class<U> impl);

    <T> boolean tryAddSingleton(Class<T> type, ServiceProviderBasedProvider<T> providerFactory);

    <T, U extends T> boolean tryAddSingleton(Class<T> type);

    <T, U extends T> boolean tryAddSingleton(Class<T> iface, Class<U> impl);

    <T> boolean tryAddScoped(Class<T> type);

    <T> boolean tryAddScoped(Class<T> type, ServiceProviderBasedProvider<T> providerFactory);

    <T, U extends T> boolean tryAddScoped(Class<T> iface, Class<U> impl);

    <T, U extends T> void addMultiBinding(Class<T> iface, Class<U> impl);

    <T, U extends T> void addScopedMultiBinding(Class<T> iface, Class<U> impl);

    <T> void remove(Class<T> type);

    boolean remove(ServiceDescriptor descriptor);

    List<ServiceDescriptor> getDescriptors();

    ServiceProvider buildServiceProvider();
}

