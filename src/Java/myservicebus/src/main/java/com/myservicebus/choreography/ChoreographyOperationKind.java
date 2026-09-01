package com.myservicebus.choreography;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;

public enum ChoreographyOperationKind {
    SEND("send"),
    PUBLISH("publish"),
    RESPOND("respond"),
    SCHEDULE("schedule"),
    TERMINAL("terminal");

    private final String value;

    ChoreographyOperationKind(String value) {
        this.value = value;
    }

    @JsonValue
    public String value() {
        return value;
    }

    @JsonCreator
    public static ChoreographyOperationKind fromValue(String value) {
        for (ChoreographyOperationKind kind : values()) {
            if (kind.value.equals(value)) {
                return kind;
            }
        }
        throw new IllegalArgumentException("Unknown choreography operation kind: " + value);
    }
}
