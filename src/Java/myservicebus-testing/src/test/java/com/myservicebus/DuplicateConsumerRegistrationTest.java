package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;

public class DuplicateConsumerRegistrationTest {
    static class Ping {
        final String value;
        Ping(String value) { this.value = value; }
    }

    static class PingConsumer implements Consumer<Ping> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<Ping> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    void duplicate_consumer_registration_is_ignored() {
        ServiceCollection services = ServiceCollection.create();
        TestingServiceExtensions.addServiceBusTestHarness(services, cfg -> {
            cfg.addConsumer(PingConsumer.class);
            cfg.addConsumer(PingConsumer.class);
        });

        ServiceProvider provider = services.buildServiceProvider();
        InMemoryTestHarness harness = provider.getService(InMemoryTestHarness.class);

        harness.start().join();
        harness.send(new Ping("hi")).join();

        long count = harness.getConsumed().stream().filter(Ping.class::isInstance).count();
        assertEquals(1, count);
    }
}
