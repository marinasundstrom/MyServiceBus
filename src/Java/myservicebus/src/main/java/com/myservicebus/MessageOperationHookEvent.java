package com.myservicebus;

import java.time.Instant;

public record MessageOperationHookEvent(
        Instant occurredAtUtc,
        String kind,
        boolean succeeded,
        String messageType,
        String messageUrn,
        String endpointName,
        String destinationAddress,
        double durationMs,
        String exceptionType,
        String exceptionMessage,
        String correlationId,
        String conversationId,
        String traceId,
        String spanId) implements BusHookEvent {

    public static MessageOperationHookEvent create(
            String kind,
            boolean succeeded,
            Class<?> messageType,
            String endpointName,
            String destinationAddress,
            long startedAtNanos,
            Throwable exception,
            String correlationId,
            String conversationId) {
        return new MessageOperationHookEvent(
                Instant.now(),
                kind,
                succeeded,
                messageType.getName(),
                MessageUrn.forClass(messageType),
                endpointName,
                destinationAddress,
                (System.nanoTime() - startedAtNanos) / 1_000_000.0,
                exception == null ? null : exception.getClass().getName(),
                exception == null ? null : exception.getMessage(),
                correlationId,
                conversationId,
                null,
                null);
    }
}
