package com.myservicebus;

import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Registry that maps context/message pairs to their pipelines.
 */
public class PipeRegistry {
    private final Map<Key, Pipe<?>> pipes = new ConcurrentHashMap<>();

    public <T, C extends PipeContext> void register(Class<T> messageType, Class<C> contextType, Pipe<C> pipe) {
        pipes.put(new Key(messageType, contextType), pipe);
    }

    @SuppressWarnings("unchecked")
    public <T, C extends PipeContext> CompletableFuture<Void> dispatch(C context, Class<T> messageType) {
        Pipe<C> pipe = (Pipe<C>) pipes.get(new Key(messageType, context.getClass()));
        if (pipe != null) {
            return pipe.send(context);
        }
        return CompletableFuture.completedFuture(null);
    }

    private record Key(Class<?> message, Class<?> context) {
    }
}

