package com.myservicebus.orchestration;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;

public enum SagaConcurrencyKind {
    SINGLE_PROCESS("single-process"),
    OPTIMISTIC("optimistic"),
    PESSIMISTIC("pessimistic");

    private final String value;

    SagaConcurrencyKind(String value) {
        this.value = value;
    }

    @JsonValue
    public String value() {
        return value;
    }

    @JsonCreator
    public static SagaConcurrencyKind fromValue(String value) {
        for (SagaConcurrencyKind kind : values()) {
            if (kind.value.equals(value)) {
                return kind;
            }
        }
        throw new IllegalArgumentException("Unknown saga concurrency kind: " + value);
    }
}
