package com.myservicebus;

public final class UnsupportedTransportCapabilityException extends UnsupportedOperationException {
    private final String transport;
    private final String capability;
    private final TransportCapabilitySupport actualSupport;
    private final boolean requireNative;

    public UnsupportedTransportCapabilityException(
            String transport,
            String capability,
            TransportCapabilitySupport actualSupport,
            boolean requireNative) {
        super("Transport '" + transport + "' does not satisfy required capability '" + capability
                + "': required '" + (requireNative ? "native" : "available") + "', actual '"
                + actualSupport.protocolValue() + "'.");
        this.transport = transport;
        this.capability = capability;
        this.actualSupport = actualSupport;
        this.requireNative = requireNative;
    }

    public String getTransport() {
        return transport;
    }

    public String getCapability() {
        return capability;
    }

    public TransportCapabilitySupport getActualSupport() {
        return actualSupport;
    }

    public boolean isRequireNative() {
        return requireNative;
    }
}
