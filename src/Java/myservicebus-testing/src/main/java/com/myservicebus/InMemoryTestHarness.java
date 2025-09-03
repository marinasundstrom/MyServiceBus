package com.myservicebus;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ConcurrentHashMap;
import java.util.function.Consumer;

import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.RequestClientTransport;
import com.myservicebus.Response2;
import com.myservicebus.TopologyRegistry;
import com.myservicebus.ConsumerTopology;

public class InMemoryTestHarness implements RequestClientTransport, TransportSendEndpointProvider {
    private final Map<Class<?>, List<com.myservicebus.Consumer<?>>> handlers = new ConcurrentHashMap<>();
    private final List<Object> consumed = Collections.synchronizedList(new ArrayList<>());
    private final ServiceProvider serviceProvider;
    private final ConsumeContextProvider consumeContextProvider;

    public InMemoryTestHarness() {
        this(null);
    }

    public InMemoryTestHarness(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
        this.consumeContextProvider = serviceProvider != null ? serviceProvider.getService(ConsumeContextProvider.class) : null;
    }

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
        return sendInternal(context, null, null);
    }

    private CompletableFuture<Void> sendInternal(SendContext context, String responseAddress, String faultAddress) {
        Object message = context.getMessage();
        CompletableFuture<Void> future = CompletableFuture.completedFuture(null);

        List<com.myservicebus.Consumer<?>> list = handlers.getOrDefault(message.getClass(), List.of());
        for (com.myservicebus.Consumer<?> raw : list) {
            @SuppressWarnings("unchecked")
            com.myservicebus.Consumer<Object> handler = (com.myservicebus.Consumer<Object>) raw;
            @SuppressWarnings("unchecked")
            ConsumeContext<Object> consumeContext = new ConsumeContext<>(message, context.getHeaders(), responseAddress,
                    faultAddress, context.getCancellationToken(), this);
            future = future.thenCompose(v -> {
                try {
                    if (consumeContextProvider != null) {
                        consumeContextProvider.setContext(consumeContext);
                    }
                    try {
                        return handler.consume(consumeContext).thenRun(() -> consumed.add(message));
                    } finally {
                        if (consumeContextProvider != null) {
                            consumeContextProvider.clear();
                        }
                    }
                } catch (Exception e) {
                    CompletableFuture<Void> failed = new CompletableFuture<>();
                    failed.completeExceptionally(e);
                    return failed;
                }
            });
        }

        if (serviceProvider != null) {
            TopologyRegistry registry = serviceProvider.getService(TopologyRegistry.class);
            if (registry != null) {
                for (ConsumerTopology ct : registry.getConsumers()) {
                    boolean handles = ct.getBindings().stream()
                            .anyMatch(b -> b.getMessageType().equals(message.getClass()));
                    if (!handles) {
                        continue;
                    }
                    future = future.thenCompose(v -> {
                        try (ServiceScope scope = serviceProvider.createScope()) {
                            ServiceProvider scoped = scope.getServiceProvider();
                            @SuppressWarnings("unchecked")
                            com.myservicebus.Consumer<Object> consumer = (com.myservicebus.Consumer<Object>) scoped
                                    .getService(ct.getConsumerType());
                            @SuppressWarnings("unchecked")
                            ConsumeContext<Object> consumeContext = new ConsumeContext<>(message, context.getHeaders(),
                                    responseAddress, faultAddress, context.getCancellationToken(), InMemoryTestHarness.this);
                            ConsumeContextProvider ctxProvider = scoped.getService(ConsumeContextProvider.class);
                            ctxProvider.setContext(consumeContext);
                            try {
                                return consumer.consume(consumeContext).thenRun(() -> consumed.add(message));
                            } finally {
                                ctxProvider.clear();
                            }
                        } catch (Exception e) {
                            CompletableFuture<Void> failed = new CompletableFuture<>();
                            failed.completeExceptionally(e);
                            return failed;
                        }
                    });
                }
            }
        }

        return future;
    }

    @Override
    public <TRequest, TResponse> CompletableFuture<TResponse> sendRequest(Class<TRequest> requestType, SendContext context,
            Class<TResponse> responseType) {
        CompletableFuture<TResponse> future = new CompletableFuture<>();
        com.myservicebus.Consumer<TResponse> handler = ctx -> {
            future.complete(responseType.cast(ctx.getMessage()));
            return CompletableFuture.completedFuture(null);
        };
        registerHandler(responseType, handler);
        sendInternal(context, "inmemory:response", null).exceptionally(ex -> {
            future.completeExceptionally(ex);
            return null;
        });
        return future.whenComplete((r, e) -> {
            List<com.myservicebus.Consumer<?>> list = handlers.get(responseType);
            if (list != null) {
                list.remove(handler);
            }
        });
    }

    @Override
    public <TRequest, T1, T2> CompletableFuture<Response2<T1, T2>> sendRequest(Class<TRequest> requestType,
            SendContext context, Class<T1> responseType1, Class<T2> responseType2) {
        CompletableFuture<Response2<T1, T2>> future = new CompletableFuture<>();

        com.myservicebus.Consumer<T1> h1 = ctx -> {
            future.complete(Response2.fromT1(responseType1.cast(ctx.getMessage())));
            return CompletableFuture.completedFuture(null);
        };
        com.myservicebus.Consumer<T2> h2 = ctx -> {
            future.complete(Response2.fromT2(responseType2.cast(ctx.getMessage())));
            return CompletableFuture.completedFuture(null);
        };

        registerHandler(responseType1, h1);
        registerHandler(responseType2, h2);

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
        });
    }

    public boolean wasConsumed(Class<?> type) {
        synchronized (consumed) {
            return consumed.stream().anyMatch(type::isInstance);
        }
    }

    public List<Object> getConsumed() {
        return consumed;
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        return new HarnessSendEndpoint();
    }

    class HarnessSendEndpoint implements SendEndpoint {
        @Override
        public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
            return InMemoryTestHarness.this.sendInternal(new SendContext(message, cancellationToken), null, null);
        }

        @Override
        public CompletableFuture<Void> send(SendContext context) {
            return InMemoryTestHarness.this.sendInternal(context, null, null);
        }
    }
}

