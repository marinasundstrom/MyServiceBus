package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;
import java.net.URI;
import java.util.concurrent.CopyOnWriteArrayList;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.mediator.MediatorBus;
import com.myservicebus.mediator.MediatorTransport;
import com.myservicebus.tasks.CancellationToken;

class BusHookTest {
    public record TestMessage(String value) {
    }

    public record ReactionMessage(String value) {
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
        MessageOperationHookEvent published = RecordingHook.EVENTS.stream()
                .filter(MessageOperationHookEvent.class::isInstance)
                .map(MessageOperationHookEvent.class::cast)
                .filter(operation -> operation.kind().equals("published"))
                .findFirst()
                .orElseThrow(() -> new AssertionError(RecordingHook.EVENTS.toString()));
        assertNotNull(published.messageId());
        assertEquals(new TestMessage("hello"), published.message());
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
    void consumerOutgoingOperationsReportTheConsumedMessageAsTheirCause() throws Exception {
        RecordingHook.EVENTS.clear();
        UUID triggerMessageId = UUID.randomUUID();
        BusHookDispatcher dispatcher = new BusHookDispatcher(Set.of(new RecordingHook()), null);
        SendEndpoint transport = new SendEndpoint() {
            @Override
            public <T> java.util.concurrent.CompletableFuture<Void> send(
                    T message,
                    CancellationToken cancellationToken) {
                return java.util.concurrent.CompletableFuture.completedFuture(null);
            }
        };
        SendEndpointProvider endpoints = uri -> new HookSendEndpoint(
                transport,
                URI.create(uri),
                dispatcher,
                true);
        ConsumeContext<TestMessage> context = new ConsumeContext<>(
                new TestMessage("react"),
                Map.of(),
                null,
                null,
                null,
                CancellationToken.none(),
                endpoints,
                URI.create("loopback://localhost/"),
                entityName -> "loopback://" + entityName,
                triggerMessageId,
                null,
                null,
                null,
                null);

        context.publish(new ReactionMessage("react")).join();

        MessageOperationHookEvent reaction = RecordingHook.EVENTS.stream()
                .filter(MessageOperationHookEvent.class::isInstance)
                .map(MessageOperationHookEvent.class::cast)
                .filter(operation -> operation.kind().equals("published")
                        && operation.messageType().equals(ReactionMessage.class.getName()))
                .findFirst()
                .orElseThrow();
        assertEquals(triggerMessageId.toString(), reaction.causationMessageId());
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
