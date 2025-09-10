package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;

public class MultipleConsumersTest {
    static class Ping {
        final String value;
        Ping(String value) { this.value = value; }
    }

    static class FirstConsumer implements Consumer<Ping> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<Ping> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    static class SecondConsumer implements Consumer<Ping> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<Ping> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    void multiple_consumers_receive_message() {
        ServiceCollection services = ServiceCollection.create();
        TestingServiceExtensions.addServiceBusTestHarness(services, cfg -> {
            cfg.addConsumer(FirstConsumer.class);
            cfg.addConsumer(SecondConsumer.class);
        });

        ServiceProvider provider = services.buildServiceProvider();
        InMemoryTestHarness harness = provider.getService(InMemoryTestHarness.class);

        harness.start().join();
        harness.send(new Ping("hi")).join();

        long count = harness.getConsumed().stream().filter(Ping.class::isInstance).count();
        assertEquals(2, count);
    }
}
