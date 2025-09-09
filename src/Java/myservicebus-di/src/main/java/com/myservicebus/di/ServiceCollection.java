package com.myservicebus.di;

import com.google.inject.*;
import com.google.inject.multibindings.Multibinder;
import com.google.inject.binder.ScopedBindingBuilder;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import java.util.function.Consumer;

import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.Slf4jLoggerFactory;
import com.myservicebus.logging.ConsoleLoggerConfig;
import com.myservicebus.logging.ConsoleLoggerFactory;

/**
 * Collects service registrations using {@link ServiceDescriptor} entries. The
 * collection can be
 * iterated to build a {@link ServiceProvider} backed by any IoC container.
 */
public class ServiceCollection implements Iterable<ServiceDescriptor> {
    private final List<ServiceDescriptor> descriptors = new ArrayList<>();
    private final PerMessageScope perMessageScope = new PerMessageScope();
    private boolean built;
    private boolean loggerFactoryRegistered;

    public <T extends ServiceCollection> T from(Class<T> decoratorType) {
        try {
            return decoratorType.getConstructor(ServiceCollection.class).newInstance(this);
        } catch (ReflectiveOperationException ex) {
            throw new IllegalArgumentException(
                    "Decorator must have a constructor(ServiceCollection)", ex);
        }
    }

    @Override
    public Iterator<ServiceDescriptor> iterator() {
        return descriptors.iterator();
    }

    public <T> void addSingleton(Class<T> type, ServiceProviderBasedProvider<T> providerFactory) {
        addDescriptor(new ServiceDescriptor(type, null, providerFactory, null, ServiceLifetime.SINGLETON, false));
    }

    public <T, U extends T> void addSingleton(Class<T> type) {
        addDescriptor(new ServiceDescriptor(type, type, null, null, ServiceLifetime.SINGLETON, false));
    }

    public <T, U extends T> void addSingleton(Class<T> iface, Class<U> impl) {
        addDescriptor(new ServiceDescriptor(iface, impl, null, null, ServiceLifetime.SINGLETON, false));
    }

    public <T> void addScoped(Class<T> type) {
        addDescriptor(new ServiceDescriptor(type, type, null, null, ServiceLifetime.SCOPED, false));
    }

    public <T> void addScoped(Class<T> type, ServiceProviderBasedProvider<T> providerFactory) {
        addDescriptor(new ServiceDescriptor(type, null, providerFactory, null, ServiceLifetime.SCOPED, false));
    }

    public <T, U extends T> void addScoped(Class<T> iface, Class<U> impl) {
        addDescriptor(new ServiceDescriptor(iface, impl, null, null, ServiceLifetime.SCOPED, false));
    }

    public <T, U extends T> void addMultiBinding(Class<T> iface, Class<U> impl) {
        addDescriptor(new ServiceDescriptor(iface, impl, null, null, ServiceLifetime.TRANSIENT, true));
    }

    public <T, U extends T> void addScopedMultiBinding(Class<T> iface, Class<U> impl) {
        addDescriptor(new ServiceDescriptor(iface, impl, null, null, ServiceLifetime.SCOPED, true));
    }

    public void addConsoleLogger() {
        addConsoleLogger(c -> {
        });
    }

    public void addConsoleLogger(Consumer<ConsoleLoggerConfig> configure) {
        if (built) {
            throw new IllegalStateException("Cannot add service to container that has been built.");
        }

        ConsoleLoggerConfig config = new ConsoleLoggerConfig();
        configure.accept(config);

        descriptors.add(
                new ServiceDescriptor(ConsoleLoggerConfig.class, null, null, config, ServiceLifetime.SINGLETON, false));
        descriptors.add(new ServiceDescriptor(LoggerFactory.class, ConsoleLoggerFactory.class, null, null,
                ServiceLifetime.SINGLETON, false));

        loggerFactoryRegistered = true;
    }

    public void addSlf4jLogger() {
        if (built) {
            throw new IllegalStateException("Cannot add service to container that has been built.");
        }

        descriptors.add(new ServiceDescriptor(LoggerFactory.class, Slf4jLoggerFactory.class, null, null,
                ServiceLifetime.SINGLETON, false));

        loggerFactoryRegistered = true;
    }

    public <T> void remove(Class<T> type) {
        if (built) {
            throw new IllegalStateException("Cannot remove service from container that has been built.");
        }

        descriptors.removeIf(d -> type.equals(d.getServiceType()));
    }

    public List<ServiceDescriptor> getDescriptors() {
        return List.copyOf(descriptors);
    }

    public ServiceProvider buildServiceProvider() {
        if (built) {
            throw new IllegalStateException("ServiceCollection.build() called more than once.");
        }
        built = true;

        MutableHolder<ServiceProvider> holder = new MutableHolder<>();

        List<ServiceDescriptor> effective = new ArrayList<>(descriptors);
        if (!loggerFactoryRegistered) {
            effective.add(new ServiceDescriptor(LoggerFactory.class, Slf4jLoggerFactory.class, null, null,
                    ServiceLifetime.SINGLETON, false));
        }

        List<com.google.inject.Module> modules = new ArrayList<>();
        List<ServiceDescriptor> deferred = new ArrayList<>();

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bindScope(Scoped.class, perMessageScope);
                bind(PerMessageScope.class).toInstance(perMessageScope);
            }
        });

        for (ServiceDescriptor d : effective) {
            if (d.getImplementationFactory() != null) {
                deferred.add(d);
            } else {
                modules.add(createModule(d));
            }
        }

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(ServiceProvider.class).toProvider(holder::get);
            }
        });

        Injector injector = Guice.createInjector(modules);

        ServiceProviderImpl provider = new ServiceProviderImpl(injector, perMessageScope);
        holder.set(provider);

        if (!deferred.isEmpty()) {
            List<com.google.inject.Module> deferredModules = new ArrayList<>();
            for (ServiceDescriptor d : deferred) {
                deferredModules.add(createDeferredModule(d, holder.get()));
            }
            injector = injector.createChildInjector(deferredModules);
            provider.setInjector(injector);
        }

        return provider;
    }

    public ServiceProvider connectAndBuild(Injector parentInjector) {
        if (built) {
            throw new IllegalStateException("ServiceCollection.build() called more than once.");
        }
        built = true;

        MutableHolder<ServiceProvider> holder = new MutableHolder<>();

        List<ServiceDescriptor> effective = new ArrayList<>(descriptors);
        if (!loggerFactoryRegistered) {
            effective.add(new ServiceDescriptor(LoggerFactory.class, Slf4jLoggerFactory.class, null, null,
                    ServiceLifetime.SINGLETON, false));
        }

        List<com.google.inject.Module> modules = new ArrayList<>();
        List<ServiceDescriptor> deferred = new ArrayList<>();

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bindScope(Scoped.class, perMessageScope);
                bind(PerMessageScope.class).toInstance(perMessageScope);
            }
        });

        for (ServiceDescriptor d : effective) {
            if (d.getImplementationFactory() != null) {
                deferred.add(d);
            } else {
                modules.add(createModule(d));
            }
        }

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(ServiceProvider.class).toProvider(holder::get);
            }
        });

        Injector injector = parentInjector.createChildInjector(modules);

        ServiceProviderImpl provider = new ServiceProviderImpl(injector, perMessageScope);
        holder.set(provider);

        if (!deferred.isEmpty()) {
            List<com.google.inject.Module> deferredModules = new ArrayList<>();
            for (ServiceDescriptor d : deferred) {
                deferredModules.add(createDeferredModule(d, holder.get()));
            }
            injector = injector.createChildInjector(deferredModules);
            provider.setInjector(injector);
        }

        return provider;
    }

    private void addDescriptor(ServiceDescriptor descriptor) {
        if (built) {
            throw new IllegalStateException("Cannot add service to container that has been built.");
        }
        descriptors.add(descriptor);
    }

    private com.google.inject.Module createModule(ServiceDescriptor d) {
        return new AbstractModule() {
            @Override
            @SuppressWarnings({ "unchecked", "rawtypes" })
            protected void configure() {
                Class serviceType = d.getServiceType();
                if (d.isMultiBinding()) {
                    Multibinder binder = Multibinder.newSetBinder(binder(), serviceType);
                    if (d.getImplementationInstance() != null) {
                        binder.addBinding().toInstance(d.getImplementationInstance());
                    } else {
                        ScopedBindingBuilder scoped = binder.addBinding().to(d.getImplementationType());
                        applyScope(scoped, d.getLifetime());
                    }
                } else {
                    if (d.getImplementationInstance() != null) {
                        bind(serviceType).toInstance(d.getImplementationInstance());
                    } else {
                        ScopedBindingBuilder scoped;
                        if (d.getImplementationType() != null && !serviceType.equals(d.getImplementationType())) {
                            scoped = bind(serviceType).to(d.getImplementationType());
                        } else {
                            scoped = bind(serviceType);
                        }
                        applyScope(scoped, d.getLifetime());
                    }
                }
            }
        };
    }

    private com.google.inject.Module createDeferredModule(ServiceDescriptor d, ServiceProvider provider) {
        return new AbstractModule() {
            @Override
            @SuppressWarnings({ "unchecked", "rawtypes" })
            protected void configure() {
                Class serviceType = d.getServiceType();
                Provider guiceProvider = d.getImplementationFactory().create(provider);
                if (d.isMultiBinding()) {
                    Multibinder binder = Multibinder.newSetBinder(binder(), serviceType);
                    ScopedBindingBuilder scoped = binder.addBinding().toProvider(guiceProvider);
                    applyScope(scoped, d.getLifetime());
                } else {
                    ScopedBindingBuilder scoped = bind(serviceType).toProvider(guiceProvider);
                    applyScope(scoped, d.getLifetime());
                }
            }
        };
    }

    private void applyScope(ScopedBindingBuilder builder, ServiceLifetime lifetime) {
        switch (lifetime) {
            case SINGLETON -> builder.in(Scopes.SINGLETON);
            case SCOPED -> builder.in(Scoped.class);
            default -> {
            }
        }
    }
}
