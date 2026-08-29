package com.myservicebus.rabbitmq;

import static org.junit.jupiter.api.Assertions.*;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicReference;
import java.lang.reflect.Field;
import java.util.function.Function;

import org.junit.jupiter.api.Test;

import com.myservicebus.*;
import com.myservicebus.MessageEntityNameFormatterSpecific;
import com.myservicebus.topology.*;
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

    interface ExternalDependency {
    }

    static class ExternalConsumer implements Consumer<MyMessage> {
        ExternalConsumer(ExternalDependency dependency) {
        }

        @Override
        public CompletableFuture<Void> consume(ConsumeContext<MyMessage> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    @MessageConsumer("attribute-orders")
    static class ExplicitEndpointConsumer implements Consumer<MyMessage> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<MyMessage> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    public void factoryConfiguresNonDefaultPort() {
        RabbitMqFactoryConfigurator configurator = new RabbitMqFactoryConfigurator();

        configurator.host("container-host", 32789);

        assertEquals("container-host", configurator.getClientHost());
        assertEquals(32789, configurator.getClientPort());
    }

    @Test
    public void factoryRegistersConsumerWithoutBindingItToTheInternalProvider() throws Exception {
        AtomicReference<Class<?>> requestedConsumerType = new AtomicReference<>();
        RabbitMqFactoryConfigurator configurator = new RabbitMqFactoryConfigurator();
        configurator.message(MyMessage.class, message -> message.setEntityName("external-message"));
        configurator.setConsumerFactory((ignored, consumerType) -> {
            requestedConsumerType.set(consumerType);
            return new ConsumerFactory() {
                @Override
                public <TConsumer, T> CompletableFuture<Void> send(
                        Class<TConsumer> type,
                        ConsumeContext<T> context,
                        Pipe<ConsumerConsumeContext<TConsumer, T>> next) {
                    return CompletableFuture.completedFuture(null);
                }
            };
        });
        configurator.receiveEndpoint("external-orders", endpoint -> {
            endpoint.prefetchCount(12);
            endpoint.concurrentMessageLimit(4);
            endpoint.setQueueArgument("x-queue-type", "quorum");
            endpoint.consumer(MyMessage.class, ExternalConsumer.class);
        });

        ServiceCollection services = ServiceCollection.create();
        configurator.configure(services);
        ServiceProvider provider = services.buildServiceProvider();
        MessageBusImpl bus = (MessageBusImpl) provider.getService(MessageBus.class);
        ConsumerTopology definition = provider.getService(TopologyRegistry.class).getConsumers().stream()
                .filter(candidate -> candidate.getConsumerType().equals(ExternalConsumer.class))
                .findFirst()
                .orElseThrow();

        assertEquals("external-orders", definition.getQueueName());
        assertEquals("external-message", definition.getBindings().get(0).getEntityName());
        assertEquals(12, definition.getPrefetchCount());
        assertEquals(4, definition.getConcurrentMessageLimit());
        assertEquals("quorum", definition.getQueueArguments().get("x-queue-type"));

        Field factoryField = MessageBusImpl.class.getDeclaredField("consumerFactoryFactory");
        factoryField.setAccessible(true);
        @SuppressWarnings("unchecked")
        Function<Class<?>, ConsumerFactory> factory =
                (Function<Class<?>, ConsumerFactory>) factoryField.get(bus);
        factory.apply(ExternalConsumer.class);

        assertEquals(ExternalConsumer.class, requestedConsumerType.get());
    }

    @Test
    public void consumerDefinitionReflectsCustomQueueAndExchange() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(services);
        cfg.addConsumer(MyConsumer.class);

        RabbitMqTransport.configure(cfg);
        cfg.complete();

        ServiceProvider provider = services.buildServiceProvider();
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

    static class StaticFormatter<T> implements MessageEntityNameFormatterSpecific<T> {
        @Override
        public String formatEntityName() {
            return "formatted-" + MyMessage.class.getSimpleName().toLowerCase();
        }
    }

    @Test
    public void messageUsesEntityNameFormatter() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(services);
        cfg.addConsumer(MyConsumer.class);

        RabbitMqTransport.configure(cfg);
        cfg.complete();

        ServiceProvider provider = services.buildServiceProvider();
        BusRegistrationContext context = new BusRegistrationContext(provider);
        RabbitMqFactoryConfigurator factoryConfigurator = provider.getService(RabbitMqFactoryConfigurator.class);

        factoryConfigurator.message(MyMessage.class, m -> m.setEntityNameFormatter(new StaticFormatter<>()));
        factoryConfigurator.receiveEndpoint("custom-queue", e -> e.configureConsumer(context, MyConsumer.class));

        TopologyRegistry registry = provider.getService(TopologyRegistry.class);
        ConsumerTopology def = registry.getConsumers().stream()
                .filter(d -> d.getConsumerType().equals(MyConsumer.class))
                .findFirst()
                .orElseThrow();

        assertEquals("formatted-mymessage", def.getBindings().get(0).getEntityName());
    }

    @Test
    public void receiveEndpointAddsMessageRetry() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(services);
        cfg.addConsumer(MyConsumer.class);

        RabbitMqTransport.configure(cfg);
        cfg.complete();

        ServiceProvider provider = services.buildServiceProvider();
        BusRegistrationContext context = new BusRegistrationContext(provider);
        RabbitMqFactoryConfigurator factoryConfigurator = provider.getService(RabbitMqFactoryConfigurator.class);

        factoryConfigurator.receiveEndpoint("custom-queue", e -> {
            e.useMessageRetry(r -> r.immediate(2));
            e.configureConsumer(context, MyConsumer.class);
        });

        TopologyRegistry registry = provider.getService(TopologyRegistry.class);
        ConsumerTopology def = registry.getConsumers().stream()
                .filter(d -> d.getConsumerType().equals(MyConsumer.class))
                .findFirst()
                .orElseThrow();

        assertNotNull(def.getConfigure());
    }

    @Test
    public void receiveEndpointSetsQueueArguments() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(services);
        cfg.addConsumer(MyConsumer.class);

        RabbitMqTransport.configure(cfg);
        cfg.complete();

        ServiceProvider provider = services.buildServiceProvider();
        BusRegistrationContext context = new BusRegistrationContext(provider);
        RabbitMqFactoryConfigurator factoryConfigurator = provider.getService(RabbitMqFactoryConfigurator.class);

        factoryConfigurator.receiveEndpoint("custom-queue", e -> {
            e.setQueueArgument("x-queue-type", "quorum");
            e.configureConsumer(context, MyConsumer.class);
        });

        TopologyRegistry registry = provider.getService(TopologyRegistry.class);
        ConsumerTopology def = registry.getConsumers().stream()
                .filter(d -> d.getConsumerType().equals(MyConsumer.class))
                .findFirst()
                .orElseThrow();

        assertEquals("quorum", def.getQueueArguments().get("x-queue-type"));
    }

    @Test
    public void configureEndpointsUsesFormatter() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(services);
        cfg.addConsumer(MyConsumer.class);

        RabbitMqTransport.configure(cfg);
        cfg.complete();

        ServiceProvider provider = services.buildServiceProvider();
        BusRegistrationContext context = new BusRegistrationContext(provider);
        RabbitMqFactoryConfigurator factoryConfigurator = provider.getService(RabbitMqFactoryConfigurator.class);

        factoryConfigurator.setEndpointNameFormatter(mt -> "formatted-" + mt.getSimpleName().toLowerCase());
        factoryConfigurator.configureEndpoints(context);

        TopologyRegistry registry = provider.getService(TopologyRegistry.class);
        ConsumerTopology def = registry.getConsumers().stream()
                .filter(d -> d.getConsumerType().equals(MyConsumer.class))
                .findFirst()
                .orElseThrow();

        assertEquals("formatted-myconsumer", def.getQueueName());
    }

    @Test
    public void configureEndpointsDoesNotReplaceExplicitEndpoint() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(services);
        cfg.addConsumer(ExplicitEndpointConsumer.class);

        RabbitMqTransport.configure(cfg);
        cfg.complete();

        ServiceProvider provider = services.buildServiceProvider();
        BusRegistrationContext context = new BusRegistrationContext(provider);
        RabbitMqFactoryConfigurator factoryConfigurator = provider.getService(RabbitMqFactoryConfigurator.class);

        factoryConfigurator.setEndpointNameFormatter(mt -> "formatted-" + mt.getSimpleName().toLowerCase());
        factoryConfigurator.configureEndpoints(context);

        TopologyRegistry registry = provider.getService(TopologyRegistry.class);
        ConsumerTopology definition = registry.getConsumers().stream()
                .filter(consumer -> consumer.getConsumerType().equals(ExplicitEndpointConsumer.class))
                .findFirst()
                .orElseThrow();

        assertEquals("attribute-orders", definition.getQueueName());
    }
}
