package com.myservicebus;

import java.util.Map;
import java.util.concurrent.CompletableFuture;

import io.opentelemetry.api.GlobalOpenTelemetry;
import io.opentelemetry.api.trace.Span;
import io.opentelemetry.api.trace.SpanKind;
import io.opentelemetry.api.trace.Tracer;
import io.opentelemetry.context.Context;
import io.opentelemetry.context.Scope;
import io.opentelemetry.context.propagation.TextMapGetter;
import io.opentelemetry.context.propagation.TextMapPropagator;

public class OpenTelemetryConsumeFilter<T> implements Filter<ConsumeContext<T>> {
    private static final Tracer tracer = GlobalOpenTelemetry.getTracer("MyServiceBus");
    private static final TextMapPropagator propagator = GlobalOpenTelemetry.getPropagators().getTextMapPropagator();

    private static final TextMapGetter<Map<String, Object>> getter = new TextMapGetter<>() {
        @Override
        public Iterable<String> keys(Map<String, Object> carrier) {
            return carrier.keySet();
        }

        @Override
        public String get(Map<String, Object> carrier, String key) {
            Object value = carrier.get(key);
            return value instanceof String ? (String) value : null;
        }
    };

    @Override
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
        Context parent = propagator.extract(Context.current(), context.getHeaders(), getter);
        Span span = tracer.spanBuilder("consume").setSpanKind(SpanKind.CONSUMER).setParent(parent).startSpan();
        try (Scope scope = span.makeCurrent()) {
            return next.send(context).whenComplete((v, ex) -> {
                if (ex != null) span.recordException(ex);
                span.end();
            });
        }
    }
}
