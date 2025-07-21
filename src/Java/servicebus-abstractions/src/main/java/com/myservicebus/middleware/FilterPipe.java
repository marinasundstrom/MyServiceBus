package com.myservicebus.middleware;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.contexts.PipeContext;

public class FilterPipe<TContext extends PipeContext> implements Pipe<TContext> {
    private final Filter<TContext> _filter;
    private final Pipe<TContext> _next;

    public FilterPipe(Filter<TContext> filter, Pipe<TContext> next) {
        _filter = filter;
        _next = next;
    }

    public CompletableFuture<Void> send(TContext context) throws Exception {
        return _filter.send(context, _next);
    }
}