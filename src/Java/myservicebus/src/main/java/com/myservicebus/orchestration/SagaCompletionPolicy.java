package com.myservicebus.orchestration;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;

public enum SagaCompletionPolicy {
    RETAIN("retain"),
    DELETE_WHEN_FINALIZED("delete-when-finalized");

    private final String value;

    SagaCompletionPolicy(String value) {
        this.value = value;
    }

    @JsonValue
    public String value() {
        return value;
    }

    @JsonCreator
    public static SagaCompletionPolicy fromValue(String value) {
        for (SagaCompletionPolicy policy : values()) {
            if (policy.value.equals(value)) {
                return policy;
            }
        }
        throw new IllegalArgumentException("Unknown saga completion policy: " + value);
    }
}
