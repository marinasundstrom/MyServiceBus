package com.myservicebus.di;

import java.util.Collections;
import java.util.IdentityHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

final class AotServiceProvider implements ServiceProvider {
    private final List<ServiceDescriptor> descriptors;
    private final Map<ServiceDescriptor, Object> singletons;
    private final Map<ServiceDescriptor, Object> scoped;
    private final boolean root;

    AotServiceProvider(List<ServiceDescriptor> descriptors) {
        this(descriptors, new ConcurrentHashMap<>(), null, true);
    }

    private AotServiceProvider(
            List<ServiceDescriptor> descriptors,
            Map<ServiceDescriptor, Object> singletons,
            Map<ServiceDescriptor, Object> scoped,
            boolean root) {
        this.descriptors = descriptors;
        this.singletons = singletons;
        this.scoped = scoped;
        this.root = root;
    }

    @Override
    public <T> T getService(Class<T> type) {
        for (int index = descriptors.size() - 1; index >= 0; index--) {
            ServiceDescriptor descriptor = descriptors.get(index);
            if (!descriptor.isMultiBinding() && descriptor.getServiceType().equals(type)) {
                return type.cast(resolve(descriptor));
            }
        }
        return null;
    }

    @Override
    public <T> Set<T> getServices(Class<T> iface) {
        Set<T> services = new LinkedHashSet<>();
        for (ServiceDescriptor descriptor : descriptors) {
            if (descriptor.isMultiBinding() && descriptor.getServiceType().equals(iface)) {
                services.add(iface.cast(resolve(descriptor)));
            }
        }
        return Collections.unmodifiableSet(services);
    }

    @Override
    public ServiceScope createScope() {
        Map<ServiceDescriptor, Object> scopeInstances = new ConcurrentHashMap<>();
        AotServiceProvider provider = new AotServiceProvider(
                descriptors,
                singletons,
                scopeInstances,
                false);
        return new ServiceScope(provider, () -> closeInstances(scopeInstances));
    }

    private Object resolve(ServiceDescriptor descriptor) {
        return switch (descriptor.getLifetime()) {
            case SINGLETON -> resolveCached(singletons, descriptor);
            case SCOPED -> {
                if (root || scoped == null) {
                    throw new IllegalStateException(
                            "Scoped service " + descriptor.getServiceType().getName()
                                    + " must be resolved from a service scope.");
                }
                yield resolveCached(scoped, descriptor);
            }
            case TRANSIENT -> create(descriptor);
        };
    }

    private Object resolveCached(Map<ServiceDescriptor, Object> cache, ServiceDescriptor descriptor) {
        Object existing = cache.get(descriptor);
        if (existing != null) {
            return existing;
        }
        synchronized (cache) {
            existing = cache.get(descriptor);
            if (existing != null) {
                return existing;
            }
            Object created = create(descriptor);
            cache.put(descriptor, created);
            return created;
        }
    }

    private Object create(ServiceDescriptor descriptor) {
        if (descriptor.getImplementationInstance() != null) {
            return descriptor.getImplementationInstance();
        }
        if (descriptor.getImplementationFactory() != null) {
            return descriptor.getImplementationFactory().create(this).get();
        }
        Class<?> implementationType = descriptor.getImplementationType();
        String implementationName = implementationType != null
                ? implementationType.getName()
                : descriptor.getServiceType().getName();
        throw new IllegalStateException(
                "AOT service " + descriptor.getServiceType().getName()
                        + " requires an explicit provider factory for " + implementationName + ".");
    }

    private static void closeInstances(Map<ServiceDescriptor, Object> instances) {
        Set<Object> disposed = Collections.newSetFromMap(new IdentityHashMap<>());
        for (Object instance : instances.values()) {
            if (instance instanceof AutoCloseable closeable && disposed.add(instance)) {
                try {
                    closeable.close();
                } catch (Exception exception) {
                    throw new IllegalStateException(
                            "Failed to close scoped service " + instance.getClass().getName(),
                            exception);
                }
            }
        }
    }
}
