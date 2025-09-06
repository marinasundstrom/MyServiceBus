package com.myservicebus;

import java.util.List;
import java.util.concurrent.CompletableFuture;

import io.opentelemetry.api.GlobalOpenTelemetry;
import io.opentelemetry.api.trace.Span;
import io.opentelemetry.api.trace.SpanKind;
import io.opentelemetry.api.trace.Tracer;
import io.opentelemetry.context.Context;
import io.opentelemetry.context.Scope;
import io.opentelemetry.context.propagation.TextMapPropagator;

public class OpenTelemetryConsumeFilter<T> implements Filter<ConsumeContext<T>> {
    private static final Tracer tracer = GlobalOpenTelemetry.getTracer("MyServiceBus");
    private static final TextMapPropagator propagator = GlobalOpenTelemetry.getPropagators().getTextMapPropagator();

    @Override
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
        Context parent = propagator.extract(Context.current(), context.getHeaders(), (carrier, key) -> {
            Object value = carrier.get(key);
            return value instanceof String s ? List.of(s) : List.of();
        });
        Span span = tracer.spanBuilder("consume").setSpanKind(SpanKind.CONSUMER).setParent(parent).startSpan();
        try (Scope scope = span.makeCurrent()) {
            return next.send(context).whenComplete((v, ex) -> {
                if (ex != null) span.recordException(ex);
                span.end();
            });
        }
    }
}
