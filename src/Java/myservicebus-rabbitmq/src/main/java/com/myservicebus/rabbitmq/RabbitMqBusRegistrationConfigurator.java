package com.myservicebus.rabbitmq;

import com.myservicebus.BusRegistrationConfigurator;
import java.util.function.Consumer;

public interface RabbitMqBusRegistrationConfigurator extends BusRegistrationConfigurator {
    void host(String host, Consumer<RabbitMqHostConfigurator> configure);
}

