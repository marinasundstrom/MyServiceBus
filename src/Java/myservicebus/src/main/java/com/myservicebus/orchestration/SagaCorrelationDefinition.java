package com.myservicebus.orchestration;

/** Describes how one event identifies its saga instance. */
public record SagaCorrelationDefinition(
        SagaCorrelationKind kind,
        String sagaMember,
        String messageMember) {

    void validate(String eventId) {
        if (kind != SagaCorrelationKind.IDENTITY) {
            throw new IllegalStateException(
                    "Saga event '" + eventId + "' uses an unsupported correlation kind.");
        }
        SagaStateMachineDefinition.required(sagaMember, "correlation.sagaMember");
        SagaStateMachineDefinition.required(messageMember, "correlation.messageMember");
    }
}
