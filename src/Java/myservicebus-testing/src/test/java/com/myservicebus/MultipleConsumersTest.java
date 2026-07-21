package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.atomic.AtomicInteger;

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
    void publishFansOutToMultipleConsumers() {
        ServiceCollection services = ServiceCollection.create();
        TestingServiceExtensions.addServiceBusTestHarness(services, cfg -> {
            cfg.addConsumer(FirstConsumer.class);
            cfg.addConsumer(SecondConsumer.class);
        });

        ServiceProvider provider = services.buildServiceProvider();
        InMemoryTestHarness harness = provider.getService(InMemoryTestHarness.class);

        harness.start().join();
        harness.publish(new Ping("hi")).join();

        long count = harness.getConsumed().stream().filter(Ping.class::isInstance).count();
        assertEquals(2, count);
        harness.stop().join();
    }

    @Test
    void directedSendReachesMultipleConsumers() {
        ServiceCollection services = ServiceCollection.create();
        TestingServiceExtensions.addServiceBusTestHarness(services, cfg -> {
            cfg.addConsumer(FirstConsumer.class);
            cfg.addConsumer(SecondConsumer.class);
        });

        ServiceProvider provider = services.buildServiceProvider();
        InMemoryTestHarness harness = provider.getService(InMemoryTestHarness.class);
        harness.start().join();

        harness.getSendEndpoint("queue:ping").send(new Ping("hi")).join();

        long count = harness.getConsumed().stream().filter(Ping.class::isInstance).count();
        assertEquals(2, count);
        harness.stop().join();
    }

    @Test
    void allConsumersAreAttemptedWhenOneFails() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        AtomicInteger successfulCalls = new AtomicInteger();
        harness.registerHandler(Ping.class,
                context -> CompletableFuture.failedFuture(new IllegalStateException("boom")));
        harness.registerHandler(Ping.class, context -> {
            successfulCalls.incrementAndGet();
            return CompletableFuture.completedFuture(null);
        });

        harness.start().join();

        assertThrows(CompletionException.class, () -> harness.send(new Ping("hi")).join());
        assertEquals(1, successfulCalls.get());
        assertEquals(1, harness.getConsumed().stream().filter(Ping.class::isInstance).count());
        harness.stop().join();
    }
}
