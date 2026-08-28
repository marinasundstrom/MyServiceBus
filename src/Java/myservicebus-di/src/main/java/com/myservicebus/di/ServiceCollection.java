package com.myservicebus.di;

import java.util.List;
import java.util.function.Supplier;

public interface ServiceCollection extends Iterable<ServiceDescriptor> {
    static ServiceCollection create() {
        return new DefaultServiceCollection();
    }

    /**
     * Creates a factory-only container that does not use Guice or reflective
     * constructor activation. Every service reached at runtime must be registered
     * with an explicit provider factory.
     */
    static ServiceCollection createAot() {
        return new AotServiceCollection();
    }

    <T extends ServiceCollection> T from(Class<T> decoratorType);

    void add(ServiceDescriptor descriptor);

    <T> void addSingleton(Class<T> type, ServiceProviderBasedProvider<T> providerFactory);

    default <T> void addSingleton(Class<T> type, Supplier<? extends T> factory) {
        addSingleton(type, ignored -> factory);
    }

    <T, U extends T> void addSingleton(Class<T> type);

    <T, U extends T> void addSingleton(Class<T> iface, Class<U> impl);

    <T> void addScoped(Class<T> type);

    <T> void addScoped(Class<T> type, ServiceProviderBasedProvider<T> providerFactory);

    default <T> void addScoped(Class<T> type, Supplier<? extends T> factory) {
        addScoped(type, ignored -> factory);
    }

    <T, U extends T> void addScoped(Class<T> iface, Class<U> impl);

    <T> boolean tryAddSingleton(Class<T> type, ServiceProviderBasedProvider<T> providerFactory);

    default <T> boolean tryAddSingleton(Class<T> type, Supplier<? extends T> factory) {
        return tryAddSingleton(type, ignored -> factory);
    }

    <T, U extends T> boolean tryAddSingleton(Class<T> type);

    <T, U extends T> boolean tryAddSingleton(Class<T> iface, Class<U> impl);

    <T> boolean tryAddScoped(Class<T> type);

    <T> boolean tryAddScoped(Class<T> type, ServiceProviderBasedProvider<T> providerFactory);

    default <T> boolean tryAddScoped(Class<T> type, Supplier<? extends T> factory) {
        return tryAddScoped(type, ignored -> factory);
    }

    <T, U extends T> boolean tryAddScoped(Class<T> iface, Class<U> impl);

    <T, U extends T> void addMultiBinding(Class<T> iface, Class<U> impl);

    <T, U extends T> void addScopedMultiBinding(Class<T> iface, Class<U> impl);

    <T> void remove(Class<T> type);

    boolean remove(ServiceDescriptor descriptor);

    List<ServiceDescriptor> getDescriptors();

    ServiceProvider buildServiceProvider();
}
