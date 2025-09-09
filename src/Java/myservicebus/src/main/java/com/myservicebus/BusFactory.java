package com.myservicebus;

import java.util.function.Consumer;

public interface BusFactory {
    <T extends BusFactoryConfigurator> MessageBus configure(Class<T> configuratorClass, Consumer<T> configure);
}
