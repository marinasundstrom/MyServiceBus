package com.myservicebus.rabbitmq;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertSame;

import org.junit.jupiter.api.Test;

import com.myservicebus.MessageBus;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.LoggerFactoryBuilder;
import com.myservicebus.MessageBusImpl;

import java.lang.reflect.Field;

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
        ServiceCollection services = ServiceCollection.create();
        cfg.configure(services);
        ServiceProvider provider = services.buildServiceProvider();
        MessageBus bus = provider.getService(MessageBus.class);
        assertNotNull(bus);

        LoggerFactory factory = provider.getService(LoggerFactory.class);
        assertNotNull(factory);
        assertEquals("ConsoleLogger", factory.create("test").getClass().getSimpleName());
    }

    @Test
    public void builderConfiguresLoggerAndServices() throws Exception {
        LoggerFactory lf = LoggerFactoryBuilder.create(b -> b.addConsole());
        MessageBus bus = MessageBus.factory
                .withLoggerFactory(lf)
                .configureServices(s -> s.addSingleton(String.class, sp -> () -> "hi"))
                .create(RabbitMqFactoryConfigurator.class, cfg -> cfg.host("localhost"));
        assertNotNull(bus);

        MessageBusImpl impl = (MessageBusImpl) bus;
        Field f = MessageBusImpl.class.getDeclaredField("serviceProvider");
        f.setAccessible(true);
        ServiceProvider provider = (ServiceProvider) f.get(impl);
        assertSame(lf, provider.getService(LoggerFactory.class));
        assertEquals("hi", provider.getService(String.class));
    }
}
