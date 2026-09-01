package com.myservicebus.orchestration;

import java.util.List;

/** Describes ordered handling of one event in one source state. */
public record SagaBehaviorDefinition(
        String sourceState,
        String eventId,
        List<SagaActivityDefinition> activities) {

    public SagaBehaviorDefinition {
        activities = List.copyOf(activities);
    }
}
