package com.myservicebus.di;

/**
 * Describes a service registration in the {@link ServiceCollection}.
 */
public class ServiceDescriptor {
    private final Class<?> serviceType;
    private final Class<?> implementationType;
    private final ServiceProviderBasedProvider<?> implementationFactory;
    private final Object implementationInstance;
    private final ServiceLifetime lifetime;
    private final boolean multiBinding;

    public ServiceDescriptor(Class<?> serviceType,
                             Class<?> implementationType,
                             ServiceProviderBasedProvider<?> implementationFactory,
                             Object implementationInstance,
                             ServiceLifetime lifetime,
                             boolean multiBinding) {
        this.serviceType = serviceType;
        this.implementationType = implementationType;
        this.implementationFactory = implementationFactory;
        this.implementationInstance = implementationInstance;
        this.lifetime = lifetime;
        this.multiBinding = multiBinding;
    }

    public Class<?> getServiceType() {
        return serviceType;
    }

    public Class<?> getImplementationType() {
        return implementationType;
    }

    public ServiceProviderBasedProvider<?> getImplementationFactory() {
        return implementationFactory;
    }

    public Object getImplementationInstance() {
        return implementationInstance;
    }

    public ServiceLifetime getLifetime() {
        return lifetime;
    }

    public boolean isMultiBinding() {
        return multiBinding;
    }
}

