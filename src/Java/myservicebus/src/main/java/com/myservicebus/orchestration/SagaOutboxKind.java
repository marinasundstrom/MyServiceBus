package com.myservicebus.orchestration;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;

public enum SagaOutboxKind {
    LOGICAL("logical"),
    TRANSACTIONAL("transactional");

    private final String value;

    SagaOutboxKind(String value) {
        this.value = value;
    }

    @JsonValue
    public String value() {
        return value;
    }

    @JsonCreator
    public static SagaOutboxKind fromValue(String value) {
        for (SagaOutboxKind kind : values()) {
            if (kind.value.equals(value)) {
                return kind;
            }
        }
        throw new IllegalArgumentException("Unknown saga outbox kind: " + value);
    }
}
