package com.myservicebus.http;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.net.URI;
import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.*;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.TopologyRegistry;

public class HttpFactoryConfiguratorTests {
    static class MyMessage {
    }

    static class MyConsumer implements Consumer<MyMessage> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<MyMessage> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    public void maps_consumer_to_explicit_endpoint() {
        HttpFactoryConfigurator cfg = new HttpFactoryConfigurator();
        cfg.addConsumer(MyConsumer.class);
        cfg.host(URI.create("http://localhost:5000/"));
        cfg.receiveEndpoint("submit-order", e -> e.configureConsumer(MyConsumer.class));

        ServiceCollection services = ServiceCollection.create();
        cfg.configure(services);
        ServiceProvider provider = services.buildServiceProvider();

        provider.getService(MessageBus.class);

        TopologyRegistry registry = provider.getService(TopologyRegistry.class);
        ConsumerTopology def = registry.getConsumers().stream()
                .filter(d -> d.getConsumerType().equals(MyConsumer.class))
                .findFirst()
                .orElseThrow();

        assertEquals("http://localhost:5000/submit-order", def.getAddress());
        assertEquals(EntityNameFormatter.format(MyMessage.class), def.getBindings().get(0).getEntityName());
    }
}
