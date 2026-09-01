package com.myservicebus.choreography;

import java.util.List;

/** Describes one application-owned reaction to a consumed message. */
public record ChoreographyStep(
        String id,
        String triggerMessageUrn,
        String ownerComponent,
        List<ChoreographyOutput> outputs) {

    public ChoreographyStep {
        outputs = List.copyOf(outputs);
    }
}
