package com.myservicebus.orchestration;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;

public enum SagaActivityKind {
    MUTATE("mutate"),
    SEND("send"),
    PUBLISH("publish"),
    TRANSITION("transition"),
    FINALIZE("finalize"),
    IGNORE("ignore");

    private final String value;

    SagaActivityKind(String value) {
        this.value = value;
    }

    @JsonValue
    public String value() {
        return value;
    }

    @JsonCreator
    public static SagaActivityKind fromValue(String value) {
        for (SagaActivityKind kind : values()) {
            if (kind.value.equals(value)) {
                return kind;
            }
        }
        throw new IllegalArgumentException("Unknown saga activity kind: " + value);
    }
}
