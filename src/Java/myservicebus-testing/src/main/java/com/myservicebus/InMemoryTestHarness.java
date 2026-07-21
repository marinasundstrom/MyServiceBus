package com.myservicebus;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.time.Duration;
import java.time.Instant;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.function.Consumer;
import java.util.Objects;

import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.TopologyRegistry;
import com.myservicebus.serialization.MessageSerializer;

public class InMemoryTestHarness implements RequestClientTransport, TransportSendEndpointProvider, PublishEndpoint {
    private final Map<Class<?>, List<com.myservicebus.Consumer<?>>> handlers = new ConcurrentHashMap<>();
    private final List<Object> consumed = Collections.synchronizedList(new ArrayList<>());
    private final Object observationLock = new Object();
    private final List<ConsumedWaiter> consumedWaiters = new ArrayList<>();
    private final ServiceProvider serviceProvider;
    private final ConsumeContextProvider consumeContextProvider;
    private volatile boolean started;

    public InMemoryTestHarness() {
        this(null);
    }

    public InMemoryTestHarness(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
        ConsumeContextProvider provider = null;
        if (serviceProvider != null) {
            try {
                provider = serviceProvider.getService(ConsumeContextProvider.class);
            } catch (Exception ex) {
                // Ignore missing scope during construction
            }
        }
        this.consumeContextProvider = provider;
    }

    public synchronized CompletableFuture<Void> start() {
        started = true;
        return CompletableFuture.completedFuture(null);
    }

    public synchronized CompletableFuture<Void> stop() {
        started = false;
        return CompletableFuture.completedFuture(null);
    }

    public <T> void registerHandler(Class<T> type, com.myservicebus.Consumer<T> consumer) {
        handlers.computeIfAbsent(type, k -> new CopyOnWriteArrayList<>()).add(consumer);
    }

    public <T> CompletableFuture<Void> send(T message) {
        return send(new SendContext(message, CancellationToken.none()));
    }

    @Override
    public <T> CompletableFuture<Void> publish(T message, CancellationToken cancellationToken) {
        return send(new SendContext(message, cancellationToken));
    }

    public <T> CompletableFuture<Void> send(T message, Consumer<SendContext> contextCallback) {
        SendContext ctx = new SendContext(message, CancellationToken.none());
        contextCallback.accept(ctx);
        return send(ctx);
    }

    public <T> CompletableFuture<Void> send(SendContext context) {
        if (!started) {
            return notStartedFuture();
        }

        Instant scheduled = context.getScheduledEnqueueTime();
        if (scheduled != null) {
            Duration delay = Duration.between(Instant.now(), scheduled);
            if (delay.isNegative()) {
                delay = Duration.ZERO;
            }
            CompletableFuture<Void> future = new CompletableFuture<>();
            CompletableFuture.delayedExecutor(delay.toMillis(), TimeUnit.MILLISECONDS)
                    .execute(() -> sendInternal(context, null, null)
                            .whenComplete((r, e) -> {
                                if (e != null) {
                                    future.completeExceptionally(e);
                                } else {
                                    future.complete(r);
                                }
                            }));
            return future;
        }
        return sendInternal(context, null, null);
    }

    private CompletableFuture<Void> sendInternal(SendContext context, String responseAddress, String faultAddress) {
        if (!started) {
            return notStartedFuture();
        }

        Object message = context.getMessage();
        Class<?> messageType = message.getClass();
        if (java.lang.reflect.Proxy.isProxyClass(messageType) && messageType.getInterfaces().length > 0) {
            messageType = messageType.getInterfaces()[0];
        }
        final Class<?> mt = messageType;
        List<CompletableFuture<Void>> deliveries = new ArrayList<>();

        List<com.myservicebus.Consumer<?>> list = handlers.entrySet().stream()
                .filter(entry -> entry.getKey().isAssignableFrom(mt))
                .flatMap(entry -> entry.getValue().stream())
                .distinct()
                .toList();
        for (com.myservicebus.Consumer<?> raw : list) {
            @SuppressWarnings("unchecked")
            com.myservicebus.Consumer<Object> handler = (com.myservicebus.Consumer<Object>) raw;
            @SuppressWarnings("unchecked")
            ConsumeContext<Object> consumeContext = new ConsumeContext<>(message, context.getHeaders(), responseAddress,
                    faultAddress, null, context.getCancellationToken(), this, java.net.URI.create("inmemory:bus"),
                    entityName -> "inmemory:" + entityName, context.getRequestId(), context.getCorrelationId(),
                    context.getConversationId(), context.getInitiatorId());
            try {
                CompletableFuture<Void> delivery;
                try {
                    if (consumeContextProvider != null) {
                        consumeContextProvider.setContext(consumeContext);
                    }
                    delivery = handler.consume(consumeContext).thenRun(() -> recordConsumed(message));
                } finally {
                    if (consumeContextProvider != null) {
                        consumeContextProvider.clear();
                    }
                }
                deliveries.add(delivery);
            } catch (Throwable failure) {
                deliveries.add(CompletableFuture.failedFuture(failure));
            }
        }

        if (serviceProvider != null) {
            TopologyRegistry registry = serviceProvider.getService(TopologyRegistry.class);
            if (registry != null) {
                for (ConsumerTopology ct : registry.getConsumers()) {
                    boolean handles = ct.getBindings().stream()
                            .anyMatch(b -> b.getMessageType().isAssignableFrom(mt));
                    if (!handles) {
                        continue;
                    }
                    ServiceScope scope = serviceProvider.createScope();
                    try {
                        ServiceProvider scoped = scope.getServiceProvider();
                        @SuppressWarnings("unchecked")
                        com.myservicebus.Consumer<Object> consumer = (com.myservicebus.Consumer<Object>) scoped
                                .getService(ct.getConsumerType());
                        @SuppressWarnings("unchecked")
                        ConsumeContext<Object> consumeContext = new ConsumeContext<>(message, context.getHeaders(),
                                responseAddress, faultAddress, null, context.getCancellationToken(),
                                InMemoryTestHarness.this, java.net.URI.create("inmemory:bus"),
                                entityName -> "inmemory:" + entityName, context.getRequestId(),
                                context.getCorrelationId(), context.getConversationId(), context.getInitiatorId());
                        ConsumeContextProvider ctxProvider = scoped.getService(ConsumeContextProvider.class);
                        ctxProvider.setContext(consumeContext);
                        CompletableFuture<Void> result;
                        try {
                            result = consumer.consume(consumeContext).thenRun(() -> recordConsumed(message));
                        } finally {
                            ctxProvider.clear();
                        }
                        scope.detach();
                        deliveries.add(result.whenComplete((ignored, failure) -> scope.close()));
                    } catch (Throwable failure) {
                        scope.close();
                        deliveries.add(CompletableFuture.failedFuture(failure));
                    }
                }
            }
        }

        return CompletableFuture.allOf(deliveries.toArray(new CompletableFuture[0]));
    }

    private static <T> CompletableFuture<T> notStartedFuture() {
        return CompletableFuture.failedFuture(
                new IllegalStateException("The in-memory test harness is not started."));
    }

    @Override
    public <TRequest, TResponse> CompletableFuture<TResponse> sendRequest(Class<TRequest> requestType,
            SendContext context,
            Class<TResponse> responseType) {
        CompletableFuture<TResponse> future = new CompletableFuture<>();
        com.myservicebus.Consumer<TResponse> handler = ctx -> {
            if (Objects.equals(context.getRequestId(), ctx.getRequestId())) {
                future.complete(responseType.cast(ctx.getMessage()));
            }
            return CompletableFuture.completedFuture(null);
        };
        com.myservicebus.Consumer<Fault> faultHandler = ctx -> {
            @SuppressWarnings("unchecked")
            Fault<TRequest> fault = (Fault<TRequest>) ctx.getMessage();
            if (Objects.equals(context.getRequestId(), ctx.getRequestId())) {
                future.completeExceptionally(new RequestFaultException(requestType.getSimpleName(), fault));
            }
            return CompletableFuture.completedFuture(null);
        };
        registerHandler(responseType, handler);
        registerHandler(Fault.class, faultHandler);
        sendInternal(context, "inmemory:response", null).exceptionally(ex -> {
            future.completeExceptionally(ex);
            return null;
        });
        return future.whenComplete((r, e) -> {
            List<com.myservicebus.Consumer<?>> list = handlers.get(responseType);
            if (list != null) {
                list.remove(handler);
            }
            List<com.myservicebus.Consumer<?>> faultList = handlers.get(Fault.class);
            if (faultList != null) {
                faultList.remove(faultHandler);
            }
        });
    }

    @Override
    public <TRequest, T1, T2> CompletableFuture<Response2<T1, T2>> sendRequest(Class<TRequest> requestType,
            SendContext context, Class<T1> responseType1, Class<T2> responseType2) {
        CompletableFuture<Response2<T1, T2>> future = new CompletableFuture<>();

        com.myservicebus.Consumer<T1> h1 = ctx -> {
            if (Objects.equals(context.getRequestId(), ctx.getRequestId())) {
                future.complete(Response2.fromT1(responseType1.cast(ctx.getMessage())));
            }
            return CompletableFuture.completedFuture(null);
        };
        com.myservicebus.Consumer<T2> h2 = ctx -> {
            if (Objects.equals(context.getRequestId(), ctx.getRequestId())) {
                future.complete(Response2.fromT2(responseType2.cast(ctx.getMessage())));
            }
            return CompletableFuture.completedFuture(null);
        };

        com.myservicebus.Consumer<Fault> faultHandler = ctx -> {
            @SuppressWarnings("unchecked")
            Fault<TRequest> fault = (Fault<TRequest>) ctx.getMessage();
            if (Objects.equals(context.getRequestId(), ctx.getRequestId())) {
                future.completeExceptionally(new RequestFaultException(requestType.getSimpleName(), fault));
            }
            return CompletableFuture.completedFuture(null);
        };

        registerHandler(responseType1, h1);
        registerHandler(responseType2, h2);
        registerHandler(Fault.class, faultHandler);

        sendInternal(context, "inmemory:response", null).exceptionally(ex -> {
            future.completeExceptionally(ex);
            return null;
        });

        return future.whenComplete((r, e) -> {
            List<com.myservicebus.Consumer<?>> list1 = handlers.get(responseType1);
            if (list1 != null) {
                list1.remove(h1);
            }
            List<com.myservicebus.Consumer<?>> list2 = handlers.get(responseType2);
            if (list2 != null) {
                list2.remove(h2);
            }
            List<com.myservicebus.Consumer<?>> faultList = handlers.get(Fault.class);
            if (faultList != null) {
                faultList.remove(faultHandler);
            }
        });
    }

    public boolean wasConsumed(Class<?> type) {
        synchronized (consumed) {
            return consumed.stream().anyMatch(type::isInstance);
        }
    }

    public CompletableFuture<Boolean> waitForConsumed(Class<?> type, Duration timeout) {
        Objects.requireNonNull(type, "type");
        Objects.requireNonNull(timeout, "timeout");
        if (timeout.isNegative()) {
            throw new IllegalArgumentException("timeout must not be negative");
        }

        ConsumedWaiter waiter;
        synchronized (observationLock) {
            if (wasConsumed(type)) {
                return CompletableFuture.completedFuture(true);
            }
            waiter = new ConsumedWaiter(type);
            consumedWaiters.add(waiter);
        }

        CompletableFuture.delayedExecutor(timeout.toMillis(), TimeUnit.MILLISECONDS)
                .execute(() -> waiter.completion.complete(false));
        waiter.completion.whenComplete((result, failure) -> {
            synchronized (observationLock) {
                consumedWaiters.remove(waiter);
            }
        });
        return waiter.completion;
    }

    private void recordConsumed(Object message) {
        consumed.add(message);

        List<ConsumedWaiter> matching;
        synchronized (observationLock) {
            matching = consumedWaiters.stream()
                    .filter(waiter -> waiter.messageType.isInstance(message))
                    .toList();
        }
        matching.forEach(waiter -> waiter.completion.complete(true));
    }

    private static final class ConsumedWaiter {
        private final Class<?> messageType;
        private final CompletableFuture<Boolean> completion = new CompletableFuture<>();

        private ConsumedWaiter(Class<?> messageType) {
            this.messageType = messageType;
        }
    }

    public List<Object> getConsumed() {
        synchronized (consumed) {
            return List.copyOf(consumed);
        }
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        return new HarnessSendEndpoint();
    }

    @Override
    public TransportSendEndpointProvider withSerializer(MessageSerializer serializer) {
        return this;
    }

    class HarnessSendEndpoint implements SendEndpoint {
        @Override
        public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
            return InMemoryTestHarness.this.sendInternal(new SendContext(message, cancellationToken), null, null);
        }

        @Override
        public CompletableFuture<Void> send(SendContext context) {
            return InMemoryTestHarness.this.send(context);
        }
    }
}
