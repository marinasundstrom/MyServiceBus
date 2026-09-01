package com.myservicebus.orchestration;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;

public enum SagaMissingInstancePolicy {
    DISCARD("discard"),
    FAULT("fault");

    private final String value;

    SagaMissingInstancePolicy(String value) {
        this.value = value;
    }

    @JsonValue
    public String value() {
        return value;
    }

    @JsonCreator
    public static SagaMissingInstancePolicy fromValue(String value) {
        for (SagaMissingInstancePolicy policy : values()) {
            if (policy.value.equals(value)) {
                return policy;
            }
        }
        throw new IllegalArgumentException("Unknown saga missing-instance policy: " + value);
    }
}
