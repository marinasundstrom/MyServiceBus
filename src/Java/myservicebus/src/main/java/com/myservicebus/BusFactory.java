package com.myservicebus;

import java.util.function.Consumer;

public interface BusFactory {
    <T extends BusFactoryConfigurator> MessageBus create(Class<T> configuratorClass, Consumer<T> configure);
}
