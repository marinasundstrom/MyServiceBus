package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;
import java.util.UUID;
import java.util.stream.IntStream;

import com.google.inject.Inject;

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

    record ParentEvent() {
    }

    record ChildEvent() {
    }

    interface AssignableEvent {
    }

    static class BaseAssignableEvent {
    }

    static class DerivedAssignableEvent extends BaseAssignableEvent implements AssignableEvent {
    }

    static class ScopedConsumerState {
        final CompletableFuture<Void> completion = new CompletableFuture<>();
        final CopyOnWriteArrayList<Object> instanceIds = new CopyOnWriteArrayList<>();
        final AtomicInteger disposeCount = new AtomicInteger();
    }

    static class ScopedAsyncConsumer implements Consumer<Ping>, AutoCloseable {
        private final ScopedConsumerState state;
        private final Object instanceId = new Object();

        @Inject
        ScopedAsyncConsumer(ScopedConsumerState state) {
            this.state = state;
        }

        @Override
        public CompletableFuture<Void> consume(ConsumeContext<Ping> context) {
            state.instanceIds.add(instanceId);
            return state.completion;
        }

        @Override
        public void close() {
            state.disposeCount.incrementAndGet();
        }
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
    void requestAndCorrelationIdentifiersFlowThroughResponse() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        AtomicReference<UUID> requestId = new AtomicReference<>();
        AtomicReference<UUID> pendingCorrelationId = new AtomicReference<>();
        AtomicReference<UUID> responseRequestId = new AtomicReference<>();
        AtomicReference<UUID> responseCorrelationId = new AtomicReference<>();
        harness.registerHandler(Ping.class, context -> {
            requestId.set(context.getRequestId());
            pendingCorrelationId.set(context.getCorrelationId());
            return context.respond(new Pong(context.getMessage().getValue()));
        });
        harness.registerHandler(Pong.class, context -> {
            responseRequestId.set(context.getRequestId());
            responseCorrelationId.set(context.getCorrelationId());
            return CompletableFuture.completedFuture(null);
        });
        harness.start().join();

        UUID correlationId = UUID.randomUUID();
        RequestClient<Ping> client = new GenericRequestClient<>(Ping.class, harness);
        Pong response = client.getResponse(
                new Ping("correlated"),
                Pong.class,
                context -> context.setCorrelationId(correlationId)).join();

        assertEquals("correlated", response.getValue());
        assertEquals(requestId.get(), responseRequestId.get());
        assertEquals(correlationId, pendingCorrelationId.get());
        assertEquals(null, responseCorrelationId.get());
        harness.stop().join();
    }

    @Test
    void concurrentRequestsMatchOnlyResponsesWithTheirRequestIdentifier() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        CopyOnWriteArrayList<ConsumeContext<Ping>> pending = new CopyOnWriteArrayList<>();
        harness.registerHandler(Ping.class, context -> {
            pending.add(context);
            if (pending.size() == 2) {
                return pending.get(1).respond(new Pong(pending.get(1).getMessage().getValue()))
                        .thenCompose(ignored -> pending.get(0)
                                .respond(new Pong(pending.get(0).getMessage().getValue())));
            }
            return CompletableFuture.completedFuture(null);
        });
        harness.start().join();
        RequestClient<Ping> client = new GenericRequestClient<>(Ping.class, harness);

        CompletableFuture<Pong> first = client.getResponse(new Ping("first"), Pong.class);
        CompletableFuture<Pong> second = client.getResponse(new Ping("second"), Pong.class);

        assertEquals("first", first.join().getValue());
        assertEquals("second", second.join().getValue());
        harness.stop().join();
    }

    @Test
    void consumerPublishInheritsCancellationAndKeepsMetadataExplicit() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        AtomicReference<Object> header = new AtomicReference<>();
        AtomicReference<UUID> correlation = new AtomicReference<>();
        AtomicReference<UUID> parentConversation = new AtomicReference<>();
        AtomicReference<UUID> conversation = new AtomicReference<>();
        AtomicReference<UUID> initiator = new AtomicReference<>();
        AtomicReference<CancellationToken> cancellation = new AtomicReference<>();
        UUID childCorrelationId = UUID.randomUUID();
        harness.registerHandler(ParentEvent.class, context -> {
            parentConversation.set(context.getConversationId());
            return context.publish(new ChildEvent(), outbound -> {
                outbound.getHeaders().put("trace-id", "child");
                outbound.setCorrelationId(childCorrelationId);
            });
        });
        harness.registerHandler(ChildEvent.class, context -> {
            header.set(context.getHeaders().get("trace-id"));
            correlation.set(context.getCorrelationId());
            conversation.set(context.getConversationId());
            initiator.set(context.getInitiatorId());
            cancellation.set(context.getCancellationToken());
            return CompletableFuture.completedFuture(null);
        });
        harness.start().join();
        com.myservicebus.tasks.CancellationTokenSource source = new com.myservicebus.tasks.CancellationTokenSource();
        UUID parentCorrelationId = UUID.randomUUID();
        SendContext context = new SendContext(new ParentEvent(), source.token());
        context.getHeaders().put("trace-id", "parent");
        context.setCorrelationId(parentCorrelationId);

        harness.send(context).join();

        assertEquals("child", header.get());
        assertEquals(childCorrelationId, correlation.get());
        assertEquals(parentConversation.get(), conversation.get());
        assertEquals(parentCorrelationId, initiator.get());
        assertEquals(source.token(), cancellation.get());
        harness.stop().join();
    }

    @Test
    void createsAndDisposesAConsumerScopePerDelivery() {
        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(ScopedConsumerState.class);
        TestingServiceExtensions.addServiceBusTestHarness(services, cfg -> {
            cfg.addConsumer(ScopedAsyncConsumer.class);
        });

        ServiceProvider provider = services.buildServiceProvider();
        InMemoryTestHarness harness = provider.getService(InMemoryTestHarness.class);
        ScopedConsumerState state = provider.getRequiredService(ScopedConsumerState.class);
        harness.start().join();

        CompletableFuture<Void> firstDelivery = harness.send(new Ping("first"));
        assertFalse(firstDelivery.isDone());
        assertEquals(0, state.disposeCount.get());

        state.completion.complete(null);
        firstDelivery.join();
        assertEquals(1, state.disposeCount.get());

        harness.send(new Ping("second")).join();
        assertEquals(2, state.disposeCount.get());
        assertEquals(2, state.instanceIds.stream().distinct().count());

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
    void dispatchesConcreteMessagesToInterfaceAndBaseHandlersOnce() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        AtomicInteger concrete = new AtomicInteger();
        AtomicInteger inherited = new AtomicInteger();
        AtomicInteger interfaceCount = new AtomicInteger();
        harness.registerHandler(DerivedAssignableEvent.class,
                context -> CompletableFuture.completedFuture(concrete.incrementAndGet()).thenApply(ignored -> null));
        harness.registerHandler(BaseAssignableEvent.class,
                context -> CompletableFuture.completedFuture(inherited.incrementAndGet()).thenApply(ignored -> null));
        harness.registerHandler(AssignableEvent.class,
                context -> CompletableFuture.completedFuture(interfaceCount.incrementAndGet()).thenApply(ignored -> null));
        harness.start().join();

        harness.send(new DerivedAssignableEvent()).join();

        assertEquals(1, concrete.get());
        assertEquals(1, inherited.get());
        assertEquals(1, interfaceCount.get());
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
