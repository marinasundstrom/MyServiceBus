package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicReference;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import io.opentelemetry.api.GlobalOpenTelemetry;
import io.opentelemetry.api.trace.Span;
import io.opentelemetry.api.trace.Tracer;
import io.opentelemetry.context.Context;
import io.opentelemetry.context.propagation.ContextPropagators;
import io.opentelemetry.context.propagation.TextMapPropagator;
import io.opentelemetry.api.trace.propagation.W3CTraceContextPropagator;
import io.opentelemetry.sdk.OpenTelemetrySdk;

class OpenTelemetryFilterTest {
    @BeforeEach
    void setup() {
        GlobalOpenTelemetry.resetForTest();
        OpenTelemetrySdk.builder()
            .setPropagators(ContextPropagators.create(W3CTraceContextPropagator.getInstance()))
            .buildAndRegisterGlobal();
    }

    @Test
    void send_filter_sets_traceparent() {
        PipeConfigurator<SendContext> cfg = new PipeConfigurator<>();
        cfg.useFilter(new OpenTelemetrySendFilter());
        Pipe<SendContext> pipe = cfg.build();
        SendContext ctx = new SendContext("hi", com.myservicebus.tasks.CancellationToken.none);
        pipe.send(ctx).join();
        assertTrue(ctx.getHeaders().containsKey("traceparent"));
    }

    @Test
    void consume_filter_links_parent() {
        Tracer tracer = GlobalOpenTelemetry.getTracer("test");
        Span parent = tracer.spanBuilder("parent").startSpan();
        Map<String, Object> headers = new HashMap<>();
        TextMapPropagator propagator = GlobalOpenTelemetry.getPropagators().getTextMapPropagator();
        propagator.inject(Context.current().with(parent), headers, Map::put);
        parent.end();

        OpenTelemetryConsumeFilter<String> filter = new OpenTelemetryConsumeFilter<>();
        AtomicReference<String> traceId = new AtomicReference<>();
        SendEndpointProvider provider = uri -> new SendEndpoint() {
            @Override
            public <T> CompletableFuture<Void> send(T message, com.myservicebus.tasks.CancellationToken token) {
                return CompletableFuture.completedFuture(null);
            }
        };
        ConsumeContext<String> ctx = new ConsumeContext<>("hi", headers, provider);
        filter.send(ctx, c -> {
            traceId.set(Span.current().getSpanContext().getTraceId());
            return CompletableFuture.completedFuture(null);
        }).join();

        assertTrue(traceId.get() != null && !traceId.get().isEmpty());
    }
}
