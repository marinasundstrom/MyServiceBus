package com.myservicebus;

import java.util.Collections;
import java.util.EnumSet;
import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;
import java.util.function.Function;

public interface Endpoint {
    <T> CompletableFuture<Void> send(T message, Consumer<SendContext> configure) throws Exception;

    default <T> CompletableFuture<Void> send(T message) throws Exception {
        return send(message, ctx -> {});
    }

    default Iterable<ConsumeContext<Object>> readAsync() {
        return Collections.emptyList();
    }

    default AutoCloseable subscribe(Function<ConsumeContext<Object>, CompletableFuture<Void>> handler) {
        return () -> {};
    }

    EnumSet<EndpointCapability> getCapabilities();
}
