package com.myservicebus;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ConcurrentHashMap;
import java.util.function.Consumer;

import com.myservicebus.tasks.CancellationToken;

public class InMemoryTestHarness {
    private final Map<Class<?>, List<com.myservicebus.Consumer<?>>> handlers = new ConcurrentHashMap<>();
    private final List<Object> consumed = Collections.synchronizedList(new ArrayList<>());

    public CompletableFuture<Void> start() {
        return CompletableFuture.completedFuture(null);
    }

    public CompletableFuture<Void> stop() {
        return CompletableFuture.completedFuture(null);
    }

    public <T> void registerHandler(Class<T> type, com.myservicebus.Consumer<T> consumer) {
        handlers.computeIfAbsent(type, k -> new ArrayList<>()).add(consumer);
    }

    public <T> CompletableFuture<Void> send(T message) {
        return send(new SendContext(message, CancellationToken.none));
    }

    public <T> CompletableFuture<Void> send(T message, Consumer<SendContext> contextCallback) {
        SendContext ctx = new SendContext(message, CancellationToken.none);
        contextCallback.accept(ctx);
        return send(ctx);
    }

    public <T> CompletableFuture<Void> send(SendContext context) {
        Object message = context.getMessage();
        List<com.myservicebus.Consumer<?>> list = handlers.getOrDefault(message.getClass(), List.of());
        CompletableFuture<Void> future = CompletableFuture.completedFuture(null);
        for (com.myservicebus.Consumer<?> raw : list) {
            @SuppressWarnings("unchecked")
            com.myservicebus.Consumer<Object> handler = (com.myservicebus.Consumer<Object>) raw;
            @SuppressWarnings("unchecked")
            ConsumeContext<Object> consumeContext = new ConsumeContext<>(message, context.getHeaders(), new HarnessSendEndpointProvider());
            future = future.thenCompose(v -> {
                try {
                    return handler.consume(consumeContext).thenRun(() -> consumed.add(message));
                } catch (Exception e) {
                    CompletableFuture<Void> failed = new CompletableFuture<>();
                    failed.completeExceptionally(e);
                    return failed;
                }
            });
        }
        return future;
    }

    public boolean wasConsumed(Class<?> type) {
        synchronized (consumed) {
            return consumed.stream().anyMatch(type::isInstance);
        }
    }

    public List<Object> getConsumed() {
        return consumed;
    }

    class HarnessSendEndpointProvider implements SendEndpointProvider {
        @Override
        public SendEndpoint getSendEndpoint(String uri) {
            return new HarnessSendEndpoint();
        }
    }

    class HarnessSendEndpoint implements SendEndpoint {
        @Override
        public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
            return InMemoryTestHarness.this.send(new SendContext(message, cancellationToken));
        }

        @Override
        public CompletableFuture<Void> send(SendContext context) {
            return InMemoryTestHarness.this.send(context);
        }
    }
}
