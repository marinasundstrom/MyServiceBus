package com.myservicebus.rabbitmq;

import static org.junit.jupiter.api.Assertions.*;

import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.*;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;

public class RabbitMqFactoryConfiguratorTests {
    static class MyMessage {
    }

    static class MyConsumer implements Consumer<MyMessage> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<MyMessage> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    public void consumerDefinitionReflectsCustomQueueAndExchange() {
        ServiceCollection services = new ServiceCollection();
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(services);
        cfg.addConsumer(MyConsumer.class);

        RabbitMqTransport.configure(cfg);
        cfg.complete();

        ServiceProvider provider = services.build();
        BusRegistrationContext context = new BusRegistrationContext(provider);
        RabbitMqFactoryConfigurator factoryConfigurator = provider.getService(RabbitMqFactoryConfigurator.class);

        factoryConfigurator.message(MyMessage.class, m -> m.setEntityName("custom-exchange"));

        factoryConfigurator.receiveEndpoint("custom-queue", e -> {
            e.configureConsumer(context, MyConsumer.class);
        });

        TopologyRegistry registry = provider.getService(TopologyRegistry.class);
        ConsumerTopology def = registry.getConsumers().stream()
                .filter(d -> d.getConsumerType().equals(MyConsumer.class))
                .findFirst()
                .orElseThrow();

        assertEquals("custom-queue", def.getQueueName());
        assertEquals("custom-exchange", def.getBindings().get(0).getEntityName());
    }

    @Test
    public void configureEndpointsUsesFormatter() {
        ServiceCollection services = new ServiceCollection();
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(services);
        cfg.addConsumer(MyConsumer.class);

        RabbitMqTransport.configure(cfg);
        cfg.complete();

        ServiceProvider provider = services.build();
        BusRegistrationContext context = new BusRegistrationContext(provider);
        RabbitMqFactoryConfigurator factoryConfigurator = provider.getService(RabbitMqFactoryConfigurator.class);

        factoryConfigurator.configureEndpoints(context, mt -> "formatted-" + mt.getSimpleName().toLowerCase());

        TopologyRegistry registry = provider.getService(TopologyRegistry.class);
        ConsumerTopology def = registry.getConsumers().stream()
                .filter(d -> d.getConsumerType().equals(MyConsumer.class))
                .findFirst()
                .orElseThrow();

        assertEquals("formatted-mymessage", def.getQueueName());
    }
}
