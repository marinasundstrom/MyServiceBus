package com.myservicebus.di;

import java.util.function.Consumer;

import com.google.inject.Injector;
import com.myservicebus.logging.ConsoleLoggerConfig;

public abstract class ServiceCollectionDecorator extends ServiceCollection {
    protected final ServiceCollection inner;

    protected ServiceCollectionDecorator(ServiceCollection inner) {
        this.inner = inner;
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
    public <T, U extends T> void addMultiBinding(Class<T> iface, Class<U> impl) {
        inner.addMultiBinding(iface, impl);
    }

    @Override
    public <T, U extends T> void addScopedMultiBinding(Class<T> iface, Class<U> impl) {
        inner.addScopedMultiBinding(iface, impl);
    }

    @Override
    public void addConsoleLogger() {
        inner.addConsoleLogger();
    }

    @Override
    public void addConsoleLogger(Consumer<ConsoleLoggerConfig> configure) {
        inner.addConsoleLogger(configure);
    }

    @Override
    public ServiceProvider buildServiceProvider() {
        return inner.buildServiceProvider();
    }

    @Override
    public ServiceProvider connectAndBuild(Injector parentInjector) {
        return inner.connectAndBuild(parentInjector);
    }
}
