package com.myservicebus.orchestration;

import java.util.List;

/** Raised when a repository cannot satisfy a state machine's declared requirements. */
public final class SagaRepositoryCapabilityException extends IllegalStateException {
    private final String provider;
    private final List<String> unsupportedCapabilities;

    public SagaRepositoryCapabilityException(String provider, List<String> unsupportedCapabilities) {
        super("Saga repository provider '" + provider + "' does not support: "
                + String.join(", ", unsupportedCapabilities) + ".");
        this.provider = provider;
        this.unsupportedCapabilities = List.copyOf(unsupportedCapabilities);
    }

    public String provider() {
        return provider;
    }

    public List<String> unsupportedCapabilities() {
        return unsupportedCapabilities;
    }
}
