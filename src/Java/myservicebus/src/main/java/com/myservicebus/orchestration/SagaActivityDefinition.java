package com.myservicebus.orchestration;

import com.fasterxml.jackson.annotation.JsonInclude;

/** Describes one ordered state-machine activity without executable application code. */
@JsonInclude(JsonInclude.Include.NON_NULL)
public record SagaActivityDefinition(
        SagaActivityKind kind,
        String activityId,
        String messageUrn,
        String destination,
        String targetState) {

    public SagaActivityDefinition(SagaActivityKind kind) {
        this(kind, null, null, null, null);
    }
}
