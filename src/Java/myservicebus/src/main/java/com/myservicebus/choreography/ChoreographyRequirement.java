package com.myservicebus.choreography;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;

public enum ChoreographyRequirement {
    INFORMATIONAL("informational"),
    OPTIONAL("optional"),
    EXPECTED("expected");

    private final String value;

    ChoreographyRequirement(String value) {
        this.value = value;
    }

    @JsonValue
    public String value() {
        return value;
    }

    @JsonCreator
    public static ChoreographyRequirement fromValue(String value) {
        for (ChoreographyRequirement requirement : values()) {
            if (requirement.value.equals(value)) {
                return requirement;
            }
        }
        throw new IllegalArgumentException("Unknown choreography requirement: " + value);
    }
}
