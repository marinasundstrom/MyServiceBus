package com.myservicebus;

import java.time.Instant;
import java.util.UUID;

public record SagaStateMachineHookEvent(
        Instant occurredAtUtc,
        boolean succeeded,
        double durationMs,
        String stateMachineId,
        String definitionVersion,
        String owner,
        String eventId,
        String status,
        UUID sagaCorrelationId,
        String beginState,
        String endState,
        boolean created,
        boolean completed,
        boolean instancePresent,
        String exceptionType,
        String exceptionMessage,
        String messageId) implements BusHookEvent {
}
