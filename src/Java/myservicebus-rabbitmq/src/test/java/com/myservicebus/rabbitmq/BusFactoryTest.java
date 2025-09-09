package com.myservicebus.rabbitmq;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;

import org.junit.jupiter.api.Test;

import com.myservicebus.MessageBus;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.logging.LoggerFactory;

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

        LoggerFactory factory = provider.getService(LoggerFactory.class);
        assertNotNull(factory);
        assertEquals("ConsoleLogger", factory.create("test").getClass().getSimpleName());
    }
}
