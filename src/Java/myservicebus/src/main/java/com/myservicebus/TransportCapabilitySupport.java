package com.myservicebus;

import com.fasterxml.jackson.annotation.JsonValue;

public enum TransportCapabilitySupport {
    NATIVE("native"),
    EMULATED("emulated"),
    UNSUPPORTED("unsupported");

    private final String protocolValue;

    TransportCapabilitySupport(String protocolValue) {
        this.protocolValue = protocolValue;
    }

    @JsonValue
    public String protocolValue() {
        return protocolValue;
    }
}
