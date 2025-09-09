package com.myservicebus.rabbitmq;

import static org.junit.jupiter.api.Assertions.assertNotNull;

import org.junit.jupiter.api.Test;

import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusServices;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;

public class ServiceCollectionUsingTest {

    @Test
    public void addServiceBusWithUsingRegistersBus() {
        ServiceCollection services = new ServiceCollection();

        services.from(MessageBusServices.class)
                .addServiceBus(cfg -> {
                    cfg.using(RabbitMqFactoryConfigurator.class, (context, rb) -> {
                    });
                });

        ServiceProvider provider = services.buildServiceProvider();
        MessageBus bus = provider.getService(MessageBus.class);
        assertNotNull(bus);
    }
}

