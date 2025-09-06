package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import io.opentelemetry.api.GlobalOpenTelemetry;
import io.opentelemetry.api.trace.Span;
import io.opentelemetry.api.trace.SpanKind;
import io.opentelemetry.api.trace.Tracer;
import io.opentelemetry.context.Scope;
import io.opentelemetry.context.Context;
import io.opentelemetry.context.propagation.TextMapPropagator;

public class OpenTelemetrySendFilter implements Filter<SendContext> {
    private static final Tracer tracer = GlobalOpenTelemetry.getTracer("MyServiceBus");
    private static final TextMapPropagator propagator = GlobalOpenTelemetry.getPropagators().getTextMapPropagator();

    @Override
    public CompletableFuture<Void> send(SendContext context, Pipe<SendContext> next) {
        Span span = tracer.spanBuilder("send").setSpanKind(SpanKind.PRODUCER).startSpan();
        try (Scope scope = span.makeCurrent()) {
            propagator.inject(Context.current(), context.getHeaders(), (c, k, v) -> c.put(k, v));
            return next.send(context).whenComplete((v, ex) -> {
                if (ex != null) span.recordException(ex);
                span.end();
            });
        }
    }
}
