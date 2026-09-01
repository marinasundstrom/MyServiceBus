package com.myservicebus.choreography;

/** Describes one possible result of a choreography reaction. */
public record ChoreographyOutput(
        ChoreographyOperationKind kind,
        String messageUrn,
        String destination,
        ChoreographyRequirement requirement,
        Integer minCount,
        Integer maxCount,
        Long withinMilliseconds) {
}
