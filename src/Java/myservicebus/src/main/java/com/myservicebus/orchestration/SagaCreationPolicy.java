package com.myservicebus.orchestration;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;

public enum SagaCreationPolicy {
    EXISTING_ONLY("existing-only"),
    IF_MISSING("if-missing");

    private final String value;

    SagaCreationPolicy(String value) {
        this.value = value;
    }

    @JsonValue
    public String value() {
        return value;
    }

    @JsonCreator
    public static SagaCreationPolicy fromValue(String value) {
        for (SagaCreationPolicy policy : values()) {
            if (policy.value.equals(value)) {
                return policy;
            }
        }
        throw new IllegalArgumentException("Unknown saga creation policy: " + value);
    }
}
