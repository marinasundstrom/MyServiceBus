package com.myservicebus.di;

import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;

/**
 * Factory-only service registrations for applications that cannot use reflective
 * constructor activation, including GraalVM Native Image applications.
 */
final class AotServiceCollection implements ServiceCollection {
    private final List<ServiceDescriptor> descriptors = new ArrayList<>();
    private boolean built;

    @Override
    public <T extends ServiceCollection> T from(Class<T> decoratorType) {
        throw new UnsupportedOperationException(
                "AOT service collection decorators must be constructed explicitly.");
    }

    @Override
    public Iterator<ServiceDescriptor> iterator() {
        return descriptors.iterator();
    }

    @Override
    public void add(ServiceDescriptor descriptor) {
        ensureMutable();
        descriptors.add(descriptor);
    }

    @Override
    public <T> void addSingleton(Class<T> type, ServiceProviderBasedProvider<T> providerFactory) {
        add(new ServiceDescriptor(type, null, providerFactory, null, ServiceLifetime.SINGLETON, false));
    }

    @Override
    public <T, U extends T> void addSingleton(Class<T> type) {
        addClassRegistration(type, type, ServiceLifetime.SINGLETON, false);
    }

    @Override
    public <T, U extends T> void addSingleton(Class<T> iface, Class<U> impl) {
        addClassRegistration(iface, impl, ServiceLifetime.SINGLETON, false);
    }

    @Override
    public <T> void addScoped(Class<T> type) {
        addClassRegistration(type, type, ServiceLifetime.SCOPED, false);
    }

    @Override
    public <T> void addScoped(Class<T> type, ServiceProviderBasedProvider<T> providerFactory) {
        add(new ServiceDescriptor(type, null, providerFactory, null, ServiceLifetime.SCOPED, false));
    }

    @Override
    public <T, U extends T> void addScoped(Class<T> iface, Class<U> impl) {
        addClassRegistration(iface, impl, ServiceLifetime.SCOPED, false);
    }

    @Override
    public <T> boolean tryAddSingleton(Class<T> type, ServiceProviderBasedProvider<T> providerFactory) {
        if (contains(type)) {
            return false;
        }
        addSingleton(type, providerFactory);
        return true;
    }

    @Override
    public <T, U extends T> boolean tryAddSingleton(Class<T> type) {
        if (contains(type)) {
            return false;
        }
        addSingleton(type);
        return true;
    }

    @Override
    public <T, U extends T> boolean tryAddSingleton(Class<T> iface, Class<U> impl) {
        if (contains(iface)) {
            return false;
        }
        addSingleton(iface, impl);
        return true;
    }

    @Override
    public <T> boolean tryAddScoped(Class<T> type) {
        if (contains(type)) {
            return false;
        }
        addScoped(type);
        return true;
    }

    @Override
    public <T> boolean tryAddScoped(Class<T> type, ServiceProviderBasedProvider<T> providerFactory) {
        if (contains(type)) {
            return false;
        }
        addScoped(type, providerFactory);
        return true;
    }

    @Override
    public <T, U extends T> boolean tryAddScoped(Class<T> iface, Class<U> impl) {
        if (contains(iface)) {
            return false;
        }
        addScoped(iface, impl);
        return true;
    }

    @Override
    public <T, U extends T> void addMultiBinding(Class<T> iface, Class<U> impl) {
        addClassRegistration(iface, impl, ServiceLifetime.TRANSIENT, true);
    }

    @Override
    public <T, U extends T> void addScopedMultiBinding(Class<T> iface, Class<U> impl) {
        addClassRegistration(iface, impl, ServiceLifetime.SCOPED, true);
    }

    @Override
    public <T> void remove(Class<T> type) {
        ensureMutable();
        descriptors.removeIf(descriptor -> type.equals(descriptor.getServiceType()));
    }

    @Override
    public boolean remove(ServiceDescriptor descriptor) {
        ensureMutable();
        return descriptors.remove(descriptor);
    }

    @Override
    public List<ServiceDescriptor> getDescriptors() {
        return List.copyOf(descriptors);
    }

    @Override
    public ServiceProvider buildServiceProvider() {
        ensureMutable();
        built = true;
        return new AotServiceProvider(List.copyOf(descriptors));
    }

    private boolean contains(Class<?> serviceType) {
        return descriptors.stream().anyMatch(descriptor ->
                serviceType.equals(descriptor.getServiceType()) && !descriptor.isMultiBinding());
    }

    private void addClassRegistration(
            Class<?> serviceType,
            Class<?> implementationType,
            ServiceLifetime lifetime,
            boolean multiBinding) {
        add(new ServiceDescriptor(
                serviceType,
                implementationType,
                null,
                null,
                lifetime,
                multiBinding));
    }

    private void ensureMutable() {
        if (built) {
            throw new IllegalStateException("Cannot modify a service collection that has been built.");
        }
    }
}
