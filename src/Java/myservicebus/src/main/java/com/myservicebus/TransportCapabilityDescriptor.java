package com.myservicebus;

import java.util.Map;

public record TransportCapabilityDescriptor(
        int version,
        String transport,
        Map<String, TransportCapabilitySupport> capabilities) {
    public TransportCapabilityDescriptor {
        if (transport == null || transport.isBlank()) {
            throw new IllegalArgumentException("transport");
        }
        capabilities = Map.copyOf(capabilities);
    }

    public TransportCapabilityDescriptor(String transport, Map<String, TransportCapabilitySupport> capabilities) {
        this(1, transport, capabilities);
    }

    public TransportCapabilitySupport get(String capability) {
        return capabilities.getOrDefault(capability, TransportCapabilitySupport.UNSUPPORTED);
    }
}
