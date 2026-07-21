package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.stream.IntStream;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.tasks.CancellationToken;

public class InMemoryHarnessDiTest {
    static class Ping {
        private final String value;

        Ping(String value) {
            this.value = value;
        }

        String getValue() {
            return value;
        }
    }

    static class Pong {
        private final String value;

        Pong(String value) {
            this.value = value;
        }

        String getValue() {
            return value;
        }
    }

    record ConcurrentMessage(int sequence) {
    }

    static class PingConsumer implements HandlerWithResult<Ping, Pong> {
        @Override
        public CompletableFuture<Pong> handle(Ping message, CancellationToken cancellationToken) {
            return CompletableFuture.completedFuture(new Pong(message.getValue()));
        }
    }

    @Test
    void request_client_round_trip() {
        ServiceCollection services = ServiceCollection.create();
        TestingServiceExtensions.addServiceBusTestHarness(services, cfg -> {
            cfg.addConsumer(PingConsumer.class);
        });

        ServiceProvider provider = services.buildServiceProvider();
        InMemoryTestHarness harness = provider.getService(InMemoryTestHarness.class);
        harness.start().join();
        try (ServiceScope scope = provider.createScope()) {
            ServiceProvider scoped = scope.getServiceProvider();
            ScopedClientFactory factory = scoped.getService(ScopedClientFactory.class);
            RequestClient<Ping> client = factory.create(Ping.class);
            Pong response = client.getResponse(new Ping("hi"), Pong.class).join();
            assertEquals("hi", response.getValue());
        }
        harness.stop().join();
    }

    @Test
    void records_concurrent_delivery_deterministically() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        harness.registerHandler(ConcurrentMessage.class, context -> CompletableFuture.completedFuture(null));
        harness.start().join();

        CompletableFuture<?>[] sends = IntStream.range(0, 200)
                .mapToObj(sequence -> harness.send(new ConcurrentMessage(sequence)))
                .toArray(CompletableFuture[]::new);
        CompletableFuture.allOf(sends).join();

        assertEquals(200, harness.getConsumed().stream()
                .filter(ConcurrentMessage.class::isInstance)
                .count());
        harness.stop().join();
    }

    @Test
    void lifecycleIsIdempotentAndOperationsRequireStartedState() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        harness.registerHandler(Ping.class, context -> CompletableFuture.completedFuture(null));

        CompletionException beforeStart = assertThrows(
                CompletionException.class,
                () -> harness.send(new Ping("before")).join());
        assertInstanceOf(IllegalStateException.class, beforeStart.getCause());

        harness.start().join();
        harness.start().join();
        harness.send(new Ping("first")).join();

        harness.stop().join();
        harness.stop().join();
        CompletionException afterStop = assertThrows(
                CompletionException.class,
                () -> harness.send(new Ping("after")).join());
        assertInstanceOf(IllegalStateException.class, afterStop.getCause());

        harness.start().join();
        harness.send(new Ping("second")).join();
        assertEquals(2, harness.getConsumed().stream().filter(Ping.class::isInstance).count());
        harness.stop().join();
    }
}
