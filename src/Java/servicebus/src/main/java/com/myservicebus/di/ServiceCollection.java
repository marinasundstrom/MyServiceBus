package com.myservicebus.di;

import com.google.inject.*;
import com.google.inject.Module;

import java.util.ArrayList;
import java.util.List;

public class ServiceCollection {
    private final List<Module> modules = new ArrayList<>();
    private final PerMessageScope perMessageScope = new PerMessageScope();
    private boolean built;

    public <T> void addSingleton(Class<T> type, Provider<T> provider) {
        if (built) {
            throw new IllegalStateException("Cannot add service to container that has been built.");
        }
        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(type).toProvider(provider).in(Scopes.SINGLETON);
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

    public <T> void addScoped(Class<T> type, Provider<T> provider) {
        if (built) {
            throw new IllegalStateException("Cannot add service to container that has been built.");
        }
        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(type).toProvider(provider).in(Scoped.class);
            }
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
                bind(ServiceProvider.class).toProvider(holder::get);
            }
        });

        Injector injector = Guice.createInjector(new ArrayList<>(modules));

        // Now create the ServiceProvider and set it
        ServiceProvider provider = new ServiceProviderImpl(injector, perMessageScope);
        holder.set(provider);

        return provider;
    }
}