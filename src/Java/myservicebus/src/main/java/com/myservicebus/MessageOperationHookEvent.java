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
        String spanId,
        Integer retryAttempt,
        Integer retryLimit,
        String messageId,
        String causationMessageId) implements BusHookEvent {

    public MessageOperationHookEvent(
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
            String spanId,
            Integer retryAttempt,
            Integer retryLimit,
            String messageId) {
        this(occurredAtUtc, kind, succeeded, messageType, messageUrn, endpointName, destinationAddress,
                durationMs, exceptionType, exceptionMessage, correlationId, conversationId, traceId, spanId,
                retryAttempt, retryLimit, messageId, null);
    }

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
        return create(kind, succeeded, messageType, endpointName, destinationAddress, startedAtNanos,
                exception, correlationId, conversationId, null, null, null);
    }

    public static MessageOperationHookEvent create(
            String kind,
            boolean succeeded,
            Class<?> messageType,
            String endpointName,
            String destinationAddress,
            long startedAtNanos,
            Throwable exception,
            String correlationId,
            String conversationId,
            Integer retryAttempt,
            Integer retryLimit) {
        return create(kind, succeeded, messageType, endpointName, destinationAddress, startedAtNanos,
                exception, correlationId, conversationId, retryAttempt, retryLimit, null, null);
    }

    public static MessageOperationHookEvent create(
            String kind,
            boolean succeeded,
            Class<?> messageType,
            String endpointName,
            String destinationAddress,
            long startedAtNanos,
            Throwable exception,
            String correlationId,
            String conversationId,
            Integer retryAttempt,
            Integer retryLimit,
            String messageId) {
        return create(kind, succeeded, messageType, endpointName, destinationAddress, startedAtNanos,
                exception, correlationId, conversationId, retryAttempt, retryLimit, messageId, null);
    }

    public static MessageOperationHookEvent create(
            String kind,
            boolean succeeded,
            Class<?> messageType,
            String endpointName,
            String destinationAddress,
            long startedAtNanos,
            Throwable exception,
            String correlationId,
            String conversationId,
            Integer retryAttempt,
            Integer retryLimit,
            String messageId,
            String causationMessageId) {
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
                null,
                retryAttempt,
                retryLimit,
                messageId,
                causationMessageId);
    }
}
