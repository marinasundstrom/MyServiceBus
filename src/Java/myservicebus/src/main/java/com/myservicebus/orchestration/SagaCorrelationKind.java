package com.myservicebus.orchestration;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;

public enum SagaCorrelationKind {
    IDENTITY("identity");

    private final String value;

    SagaCorrelationKind(String value) {
        this.value = value;
    }

    @JsonValue
    public String value() {
        return value;
    }

    @JsonCreator
    public static SagaCorrelationKind fromValue(String value) {
        for (SagaCorrelationKind kind : values()) {
            if (kind.value.equals(value)) {
                return kind;
            }
        }
        throw new IllegalArgumentException("Unknown saga correlation kind: " + value);
    }
}
