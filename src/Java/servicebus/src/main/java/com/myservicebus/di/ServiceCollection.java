package com.myservicebus.di;

import com.google.inject.*;
import com.google.inject.Module;

import java.util.ArrayList;
import java.util.List;

public class ServiceCollection {
    private final List<Module> modules = new ArrayList<>();
    private final PerMessageScope perMessageScope = new PerMessageScope();

    public <T, U extends T> void addSingleton(Class<T> iface, Class<U> impl) {
        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(iface).to(impl).in(Scopes.SINGLETON);
            }
        });
    }

    public <T> void addScoped(Class<T> type) {
        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bindScope(Scoped.class, perMessageScope);
                bind(type).in(Scoped.class);
            }
        });
    }

    public ServiceProvider build() {
        MutableHolder<ServiceProvider> holder = new MutableHolder<>();

        modules.add(new AbstractModule() {
            @Override
            protected void configure() {
                bind(PerMessageScope.class).toInstance(perMessageScope);
                bind(ServiceProvider.class).toProvider(holder::get);
            }
        });

        Injector injector = Guice.createInjector(modules);

        // Now create the ServiceProvider and set it
        ServiceProvider provider = new ServiceProviderImpl(injector, perMessageScope);
        holder.set(provider);

        return provider;
    }
}