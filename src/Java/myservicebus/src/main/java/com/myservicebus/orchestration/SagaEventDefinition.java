package com.myservicebus.orchestration;

/** Describes one message event and its instance policy. */
public record SagaEventDefinition(
        String id,
        String messageUrn,
        SagaCorrelationDefinition correlation,
        SagaCreationPolicy creationPolicy,
        SagaMissingInstancePolicy missingInstancePolicy) {
}
