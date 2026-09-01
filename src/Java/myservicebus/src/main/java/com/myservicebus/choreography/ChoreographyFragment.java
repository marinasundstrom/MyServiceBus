package com.myservicebus.choreography;

import java.util.List;
import java.util.HashSet;
import java.util.Set;

/**
 * Describes the choreography reactions owned by one application.
 *
 * <p>A fragment is local topology and monitoring metadata. It is not serialized into
 * application message envelopes and does not execute the declared reactions.</p>
 */
public record ChoreographyFragment(
        int schemaVersion,
        String choreographyId,
        String definitionVersion,
        String owner,
        List<ChoreographyStep> steps) {

    public static final int CURRENT_SCHEMA_VERSION = 1;

    public ChoreographyFragment {
        steps = List.copyOf(steps);
    }

    /** Validates the portable declaration independently of registration or execution. */
    public void validate() {
        if (schemaVersion != CURRENT_SCHEMA_VERSION) {
            throw new IllegalStateException(
                    "Unsupported choreography schema version " + schemaVersion
                            + "; expected " + CURRENT_SCHEMA_VERSION + ".");
        }
        required(choreographyId, "choreographyId");
        required(definitionVersion, "definitionVersion");
        required(owner, "owner");
        if (steps.isEmpty()) {
            throw new IllegalStateException("A choreography fragment must declare at least one step.");
        }

        Set<String> stepIds = new HashSet<>();
        for (ChoreographyStep step : steps) {
            required(step.id(), "step.id");
            required(step.triggerMessageUrn(), "step.triggerMessageUrn");
            if (step.ownerComponent() != null) {
                required(step.ownerComponent(), "step.ownerComponent");
            }
            if (!stepIds.add(step.id())) {
                throw new IllegalStateException("A choreography fragment cannot contain duplicate step IDs.");
            }
            if (step.outputs().isEmpty()) {
                throw new IllegalStateException(
                        "Choreography step '" + step.id() + "' must declare at least one output or terminal outcome.");
            }
            for (ChoreographyOutput output : step.outputs()) {
                validateOutput(step.id(), output);
            }
        }
    }

    private static void validateOutput(String stepId, ChoreographyOutput output) {
        if (output.kind() == null || output.requirement() == null) {
            throw new IllegalStateException(
                    "Choreography step '" + stepId + "' contains a null output kind or requirement.");
        }
        if ((output.minCount() != null && output.minCount() < 0)
                || (output.maxCount() != null && output.maxCount() < 0)) {
            throw new IllegalStateException(
                    "Choreography step '" + stepId + "' cannot declare a negative output count.");
        }
        if (output.minCount() != null && output.maxCount() != null
                && output.minCount() > output.maxCount()) {
            throw new IllegalStateException(
                    "Choreography step '" + stepId + "' has a minimum output count greater than its maximum.");
        }
        if (output.withinMilliseconds() != null && output.withinMilliseconds() <= 0) {
            throw new IllegalStateException(
                    "Choreography step '" + stepId + "' must use a positive timing expectation.");
        }

        if (output.kind() == ChoreographyOperationKind.TERMINAL) {
            if (output.messageUrn() != null || output.destination() != null
                    || output.minCount() != null || output.maxCount() != null
                    || output.withinMilliseconds() != null) {
                throw new IllegalStateException(
                        "Terminal outcome on choreography step '" + stepId
                                + "' cannot describe a message, destination, count, or timing expectation.");
            }
            return;
        }

        required(output.messageUrn(), "output.messageUrn");
        if (output.kind() == ChoreographyOperationKind.SEND) {
            required(output.destination(), "output.destination");
        } else if ((output.kind() == ChoreographyOperationKind.PUBLISH
                || output.kind() == ChoreographyOperationKind.RESPOND)
                && output.destination() != null) {
            throw new IllegalStateException(
                    output.kind() + " outcome on choreography step '" + stepId + "' cannot declare a destination.");
        }
    }

    private static void required(String value, String field) {
        if (value == null || value.isBlank()) {
            throw new IllegalStateException("Choreography field '" + field + "' cannot be empty or whitespace.");
        }
    }
}
