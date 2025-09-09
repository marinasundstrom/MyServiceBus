package com.myservicebus;

import java.util.function.Consumer;

public interface FactoryConfigurator<T> {
    void receiveEndpoint(String endpointName, Consumer<T> configure);
}

