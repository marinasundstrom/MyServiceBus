package com.myservicebus.middleware;

import java.util.ArrayList;

import com.myservicebus.contexts.PipeContext;

public class PipeBuilder<TContext extends PipeContext> {
    private final ArrayList<Filter<TContext>> _filters = new ArrayList<Filter<TContext>>();

    public PipeBuilder<TContext> use(Filter<TContext> filter) {
        _filters.add(filter);
        return this;
    }

    public Pipe<TContext> build() {
        Pipe<TContext> current = new TerminalPipe<TContext>();

        for (int i = _filters.size() - 1; i >= 0; i--) {
            current = new FilterPipe<TContext>(_filters.get(i), current);
        }

        return current;
    }
}