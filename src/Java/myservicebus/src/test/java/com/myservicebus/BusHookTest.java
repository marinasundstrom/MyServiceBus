package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.List;
import java.net.URI;
import java.util.concurrent.CopyOnWriteArrayList;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.mediator.MediatorBus;
import com.myservicebus.mediator.MediatorTransport;

class BusHookTest {
    public record TestMessage(String value) {
    }

    public static final class RecordingHook implements BusHook {
        static final List<BusHookEvent> EVENTS = new CopyOnWriteArrayList<>();

        @Override
        public void handle(BusHookEvent busEvent) {
            EVENTS.add(busEvent);
        }
    }

    public static final class ThrowingHook implements BusHook {
        @Override
        public void handle(BusHookEvent busEvent) {
            throw new IllegalStateException("Hook failure");
        }
    }

    public static final class RetryingConsumer implements Consumer<TestMessage> {
        static int attempts;

        @Override
        public java.util.concurrent.CompletableFuture<Void> consume(ConsumeContext<TestMessage> context) {
            attempts++;
            return java.util.concurrent.CompletableFuture.failedFuture(
                    new IllegalStateException("retry failure"));
        }
    }

    @Test
    void registeredHooksObserveLifecycleAndMessageOperations() throws Exception {
        RecordingHook.EVENTS.clear();
        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(TransportFactory.class, ignored -> () -> new NoOpTransportFactory());
        MessageBus bus = MessageBusImpl.configure(services, configurator -> {
            configurator.addHook(RecordingHook.class);
            MediatorTransport.configure(configurator);
        });

        bus.start();
        bus.publish(new TestMessage("hello")).join();
        bus.stop();

        assertTrue(RecordingHook.EVENTS.stream()
                .anyMatch(event -> event instanceof BusLifecycleHookEvent lifecycle && lifecycle.state().equals("started")));
        assertTrue(RecordingHook.EVENTS.stream()
                .anyMatch(event -> event instanceof BusLifecycleHookEvent lifecycle && lifecycle.state().equals("stopped")));
        assertTrue(RecordingHook.EVENTS.stream()
                .anyMatch(event -> event instanceof MessageOperationHookEvent operation && operation.kind().equals("published")));
    }

    @Test
    void hookFailuresDoNotChangeMessageOutcomes() throws Exception {
        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(TransportFactory.class, ignored -> () -> new NoOpTransportFactory());
        MessageBus bus = MessageBusImpl.configure(services, configurator -> {
            configurator.addHook(ThrowingHook.class);
            MediatorTransport.configure(configurator);
        });

        bus.start();
        assertDoesNotThrow(() -> bus.publish(new TestMessage("hello")).join());
        bus.stop();
    }

    @Test
    void retryHooksReportAttemptsAndExhaustion() throws Exception {
        RecordingHook.EVENTS.clear();
        RetryingConsumer.attempts = 0;
        ServiceCollection services = ServiceCollection.create();
        MediatorBus bus = MediatorBus.configure(services, configurator -> {
            configurator.addHook(RecordingHook.class);
            configurator.addConsumer(RetryingConsumer.class, TestMessage.class, pipe -> pipe.useRetry(1));
        });

        try {
            bus.publish(new TestMessage("retry"));
        } catch (java.util.concurrent.CompletionException expected) {
        }

        MessageOperationHookEvent attempted = RecordingHook.EVENTS.stream()
                .filter(MessageOperationHookEvent.class::isInstance)
                .map(MessageOperationHookEvent.class::cast)
                .filter(event -> event.kind().equals("retry_attempted"))
                .findFirst()
                .orElseThrow();
        MessageOperationHookEvent exhausted = RecordingHook.EVENTS.stream()
                .filter(MessageOperationHookEvent.class::isInstance)
                .map(MessageOperationHookEvent.class::cast)
                .filter(event -> event.kind().equals("retry_exhausted"))
                .findFirst()
                .orElseThrow();
        assertEquals(1, attempted.retryAttempt());
        assertEquals(1, attempted.retryLimit());
        assertEquals(2, exhausted.retryAttempt());
        assertEquals(1, exhausted.retryLimit());
        assertEquals(2, RetryingConsumer.attempts);
    }

    private static final class NoOpTransportFactory implements TransportFactory {
        @Override
        public SendTransport getSendTransport(URI address) {
            return (data, headers, contentType) -> {
            };
        }

        @Override
        public String getPublishAddress(String exchange) {
            return "loopback://" + exchange;
        }

        @Override
        public String getSendAddress(String queue) {
            return "loopback://" + queue;
        }
    }
}
