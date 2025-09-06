package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

class PipeRegistryTest {
    @Test
    void dispatchesPipelinePerMessageAndContext() {
        List<String> calls = new ArrayList<>();
        ConsumeFilter<String> first = (ctx, next) -> {
            calls.add("A:" + ctx.getMessage());
            return next.send(ctx);
        };
        ConsumeFilter<String> second = (ctx, next) -> {
            calls.add("B");
            return next.send(ctx);
        };
        TypedPipe<ConsumeContext<String>, String> pipe = new TypedPipe<>(List.of(first, second));
        PipeRegistry registry = new PipeRegistry();
        registry.register(String.class, ConsumeContext.class, pipe);
        SendEndpointProvider provider = uri -> (message, token) -> CompletableFuture.completedFuture(null);
        ConsumeContext<String> ctx = new ConsumeContext<>("hi", Map.of(), provider);
        registry.dispatch(ctx, String.class).join();
        assertEquals(List.of("A:hi", "B"), calls);
    }
}

