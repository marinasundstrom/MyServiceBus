package com.myservicebus.topology;

import com.myservicebus.orchestration.SagaStateMachineDefinition;

/** Describes a registered saga state machine and its receive endpoint. */
public record SagaStateMachineTopology(
        SagaStateMachineDefinition definition,
        String endpointName) {
}
