package com.myservicebus;

import java.util.function.Consumer;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.logging.LoggerFactory;

public interface BusFactory {
    <T extends BusFactoryConfigurator> MessageBus create(Class<T> configuratorClass, Consumer<T> configure);

    default BusFactoryBuilder withLoggerFactory(LoggerFactory loggerFactory) {
        return new BusFactoryBuilder(this).withLoggerFactory(loggerFactory);
    }

    default BusFactoryBuilder configureServices(Consumer<ServiceCollection> configure) {
        return new BusFactoryBuilder(this).configureServices(configure);
    }
}
