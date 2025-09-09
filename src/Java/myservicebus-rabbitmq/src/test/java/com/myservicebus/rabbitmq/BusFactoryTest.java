package com.myservicebus.rabbitmq;

import static org.junit.jupiter.api.Assertions.assertNotNull;

import org.junit.jupiter.api.Test;

import com.myservicebus.MessageBus;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;

public class BusFactoryTest {
    @Test
    public void factoryBuildsBus() {
        MessageBus bus = MessageBus.factory.create(RabbitMqFactoryConfigurator.class, cfg -> {
            cfg.host("localhost");
        });
        assertNotNull(bus);
    }

    @Test
    public void buildConfiguresServices() {
        RabbitMqFactoryConfigurator cfg = new RabbitMqFactoryConfigurator();
        cfg.host("localhost");
        ServiceCollection services = new ServiceCollection();
        cfg.configure(services);
        ServiceProvider provider = services.buildServiceProvider();
        MessageBus bus = provider.getService(MessageBus.class);
        assertNotNull(bus);
    }
}
