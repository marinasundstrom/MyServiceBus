package com.myservicebus.orchestration;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;

public enum SagaDurabilityKind {
    VOLATILE("volatile"),
    DURABLE("durable");

    private final String value;

    SagaDurabilityKind(String value) {
        this.value = value;
    }

    @JsonValue
    public String value() {
        return value;
    }

    @JsonCreator
    public static SagaDurabilityKind fromValue(String value) {
        for (SagaDurabilityKind kind : values()) {
            if (kind.value.equals(value)) {
                return kind;
            }
        }
        throw new IllegalArgumentException("Unknown saga durability kind: " + value);
    }
}
