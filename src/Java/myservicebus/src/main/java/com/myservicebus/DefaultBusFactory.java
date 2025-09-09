package com.myservicebus;

import java.util.function.Consumer;

public class DefaultBusFactory implements BusFactory {
    @Override
    public <T extends BusFactoryConfigurator> MessageBus configure(Class<T> configuratorClass, Consumer<T> configure) {
        try {
            T cfg = configuratorClass.getDeclaredConstructor().newInstance();
            if (configure != null) {
                configure.accept(cfg);
            }
            return cfg.build();
        } catch (Exception e) {
            throw new RuntimeException("Unable to create configurator", e);
        }
    }
}
