package com.myservicebus;

public final class TransportCapabilityValidator {
    private TransportCapabilityValidator() {
    }

    public static void validate(
            TransportCapabilityDescriptor descriptor,
            Iterable<TransportCapabilityRequirement> requirements) {
        for (TransportCapabilityRequirement requirement : requirements) {
            TransportCapabilitySupport actual = descriptor.get(requirement.capability());
            if (actual == TransportCapabilitySupport.UNSUPPORTED
                    || requirement.requireNative() && actual != TransportCapabilitySupport.NATIVE) {
                throw new UnsupportedTransportCapabilityException(
                        descriptor.transport(),
                        requirement.capability(),
                        actual,
                        requirement.requireNative());
            }
        }
    }
}
