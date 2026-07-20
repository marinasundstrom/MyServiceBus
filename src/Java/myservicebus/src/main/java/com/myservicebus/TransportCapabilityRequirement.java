package com.myservicebus;

public record TransportCapabilityRequirement(String capability, boolean requireNative) {
    public TransportCapabilityRequirement {
        if (capability == null || capability.isBlank()) {
            throw new IllegalArgumentException("capability");
        }
    }
}
