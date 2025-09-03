package com.myservicebus.di;

import com.google.inject.*;
import com.google.inject.Module;
import com.google.inject.multibindings.Multibinder;
import org.slf4j.Logger;

import java.util.ArrayList;
import java.util.List;
import java.util.function.Consumer;

public class ServiceCollection {
    private final List<Module> modules = new ArrayList<>();
    private final List<Module> deferredModules = new ArrayList<>();
    private final List<Consumer<ServiceProvider>> deferredScopedProviders = new ArrayList<>();
    private final PerMessageScope perMessageScope = new PerMessageScope();
    private boolean built;

    public <T> void addSingleton(Class<T> type, ServiceProviderBasedProvider<T> providerFactory) {
        if (built) {
            throw new IllegalStateException("Cannot add service to container that has been built.");
        }

        deferredScopedProviders.add(sp -> {
            deferredModules.add(new AbstractModule() {
                @Override
                protected void configure() {
                    bind(type).toProvider(providerFactory.create(sp)).in(Scopes.SINGLETON);
                }
            });
        });
    }

    public <T, U extends T> void addSingleton(Class<T> type) {
        if (built) {
            throw new IllegalStateException("Cannot add service to container that has been built.");
        }

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(type).in(Scopes.SINGLETON);
            }
        });
    }

    public <T, U extends T> void addSingleton(Class<T> iface, Class<U> impl) {
        if (built) {
            throw new IllegalStateException("Cannot add service to container that has been built.");
        }

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(iface).to(impl).in(Scopes.SINGLETON);
            }
        });
    }

    public <T> void addScoped(Class<T> type) {
        if (built) {
            throw new IllegalStateException("Cannot add service to container that has been built.");
        }

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(type).in(Scoped.class);
            }
        });
    }

    public <T> void addScoped(Class<T> type, ServiceProviderBasedProvider<T> providerFactory) {
        if (built) {
            throw new IllegalStateException("Cannot add service to container that has been built.");
        }

        deferredScopedProviders.add(sp -> {
            deferredModules.add(new AbstractModule() {
                @Override
                protected void configure() {
                    bind(type).toProvider(providerFactory.create(sp)).in(Scoped.class);
                }
            });
        });
    }

    public <T, U extends T> void addScoped(Class<T> iface, Class<U> impl) {
        if (built) {
            throw new IllegalStateException("Cannot add service to container that has been built.");
        }

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(iface).to(impl).in(Scoped.class);
            }
        });
    }

    public <T, U extends T> void addMultiBinding(Class<T> iface, Class<U> impl) {
        if (built) {
            throw new IllegalStateException("Cannot add service to container that has been built.");
        }

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                Multibinder<T> binder = Multibinder.newSetBinder(binder(), iface);
                binder.addBinding().to(impl);
            }
        });
    }

    public <T, U extends T> void addScopedMultiBinding(Class<T> iface, Class<U> impl) {
        if (built) {
            throw new IllegalStateException("Cannot add service to container that has been built.");
        }

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                Multibinder<T> binder = Multibinder.newSetBinder(binder(), iface);
                binder.addBinding().to(impl).in(Scoped.class);
            }
        });
    }

    public ServiceProvider build() {
        if (built) {
            throw new IllegalStateException("ServiceCollection.build() called more than once.");
        }
        built = true;

        MutableHolder<ServiceProvider> holder = new MutableHolder<>();

        // FIRST: Add the scope registration module
        modules.add(0, new AbstractModule() {
            @Override
            protected void configure() {
                bindScope(Scoped.class, perMessageScope); // ✅ must come before anything uses @Scoped
                bind(PerMessageScope.class).toInstance(perMessageScope);
            }
        });

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(Logger.class).toProvider(Slf4jLoggerProvider.class);
            }
        });

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(ServiceProvider.class).toProvider(holder::get);
            }
        });

        Injector injector = Guice.createInjector(new ArrayList<>(modules));

        // Now create the ServiceProvider and set it
        ServiceProviderImpl provider = new ServiceProviderImpl(injector, perMessageScope);
        holder.set(provider);

        // Apply the deferred bindings
        deferredScopedProviders.forEach(p -> p.accept(holder.get()));

        // Add the new modules with bindings
        injector = injector.createChildInjector(deferredModules); // or re-create the final injector

        provider.setInjector(injector);

        return provider;
    }

    public ServiceProvider connectAndBuild(Injector parentInjector) {
        if (built) {
            throw new IllegalStateException("ServiceCollection.build() called more than once.");
        }
        built = true;

        MutableHolder<ServiceProvider> holder = new MutableHolder<>();

        modules.add(0, new AbstractModule() {
            @Override
            protected void configure() {
                bindScope(Scoped.class, perMessageScope);
                bind(PerMessageScope.class).toInstance(perMessageScope);
            }
        });

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(Logger.class).toProvider(Slf4jLoggerProvider.class);
            }
        });

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(ServiceProvider.class).toProvider(holder::get);
            }
        });

        Injector injector = parentInjector.createChildInjector(new ArrayList<>(modules));

        ServiceProviderImpl provider = new ServiceProviderImpl(injector, perMessageScope);
        holder.set(provider);

        deferredScopedProviders.forEach(p -> p.accept(holder.get()));

        injector = injector.createChildInjector(deferredModules);

        provider.setInjector(injector);

        return provider;
    }
}