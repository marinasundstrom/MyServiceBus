package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import io.opentelemetry.api.GlobalOpenTelemetry;
import io.opentelemetry.api.trace.Span;
import io.opentelemetry.api.trace.SpanKind;
import io.opentelemetry.api.trace.Tracer;
import io.opentelemetry.context.Context;
import io.opentelemetry.context.Scope;
import io.opentelemetry.context.propagation.TextMapPropagator;

public class OpenTelemetryPublishFilter implements Filter<PublishContext> {
    @Override
    public CompletableFuture<Void> send(PublishContext context, Pipe<PublishContext> next) {
        Tracer tracer = GlobalOpenTelemetry.getTracer("MyServiceBus");
        TextMapPropagator propagator = GlobalOpenTelemetry.getPropagators().getTextMapPropagator();
        Span span = tracer.spanBuilder("send").setSpanKind(SpanKind.PRODUCER).startSpan();
        try (Scope scope = span.makeCurrent()) {
            propagator.inject(Context.current(), context.getHeaders(), (carrier, key, value) -> carrier.put(key, value));
            return next.send(context).whenComplete((ignored, failure) -> {
                if (failure != null) {
                    span.recordException(failure);
                }
                span.end();
            });
        }
    }
}
