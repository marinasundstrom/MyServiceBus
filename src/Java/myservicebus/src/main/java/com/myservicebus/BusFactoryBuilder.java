package com.myservicebus;

import java.util.ArrayList;
import java.util.List;
import java.util.function.Consumer;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.logging.LoggerFactory;

public class BusFactoryBuilder implements BusFactory {
    private final BusFactory inner;
    private LoggerFactory loggerFactory;
    private final List<Consumer<ServiceCollection>> serviceActions = new ArrayList<>();

    BusFactoryBuilder(BusFactory inner) {
        this.inner = inner;
    }

    public BusFactoryBuilder withLoggerFactory(LoggerFactory factory) {
        this.loggerFactory = factory;
        return this;
    }

    public BusFactoryBuilder configureServices(Consumer<ServiceCollection> configure) {
        this.serviceActions.add(configure);
        return this;
    }

    @Override
    public <T extends BusFactoryConfigurator> MessageBus create(Class<T> configuratorClass, Consumer<T> configure) {
        try {
            T cfg = configuratorClass.getDeclaredConstructor().newInstance();
            if (configure != null) {
                configure.accept(cfg);
            }

            ServiceCollection services = ServiceCollection.create();
            for (Consumer<ServiceCollection> action : serviceActions) {
                action.accept(services);
            }
            if (loggerFactory != null) {
                services.addSingleton(LoggerFactory.class, sp -> () -> loggerFactory);
            }

            cfg.configure(services);
            ServiceProvider provider = services.buildServiceProvider();
            return provider.getService(MessageBus.class);
        } catch (Exception e) {
            throw new RuntimeException("Unable to create configurator", e);
        }
    }
}
