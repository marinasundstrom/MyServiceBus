package com.myservicebus.choreography;

import java.util.List;

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
}
