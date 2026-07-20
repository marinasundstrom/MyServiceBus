package com.myservicebus;

import java.util.Map;

public final class TransportCapabilityDescriptors {
    public static final TransportCapabilityDescriptor RABBITMQ = new TransportCapabilityDescriptor(
            "rabbitmq",
            Map.ofEntries(
                    Map.entry(TransportCapabilities.DIRECTED_SEND, TransportCapabilitySupport.NATIVE),
                    Map.entry(TransportCapabilities.PUBLISH_SUBSCRIBE, TransportCapabilitySupport.NATIVE),
                    Map.entry(TransportCapabilities.DURABILITY, TransportCapabilitySupport.NATIVE),
                    Map.entry(TransportCapabilities.COMPETING_CONSUMERS, TransportCapabilitySupport.NATIVE),
                    Map.entry(TransportCapabilities.ACKNOWLEDGEMENT, TransportCapabilitySupport.NATIVE),
                    Map.entry(TransportCapabilities.REQUEST_RESPONSE, TransportCapabilitySupport.EMULATED),
                    Map.entry(TransportCapabilities.SCHEDULING, TransportCapabilitySupport.UNSUPPORTED),
                    Map.entry(TransportCapabilities.REDELIVERY, TransportCapabilitySupport.EMULATED),
                    Map.entry(TransportCapabilities.ERROR_DESTINATIONS, TransportCapabilitySupport.EMULATED),
                    Map.entry(TransportCapabilities.ORDERING, TransportCapabilitySupport.NATIVE),
                    Map.entry(TransportCapabilities.REPLAY, TransportCapabilitySupport.UNSUPPORTED),
                    Map.entry(TransportCapabilities.TEMPORARY_ENDPOINTS, TransportCapabilitySupport.NATIVE),
                    Map.entry(TransportCapabilities.TOPOLOGY_PROVISIONING, TransportCapabilitySupport.NATIVE)));

    public static final TransportCapabilityDescriptor IN_MEMORY = new TransportCapabilityDescriptor(
            "in-memory",
            Map.ofEntries(
                    Map.entry(TransportCapabilities.DIRECTED_SEND, TransportCapabilitySupport.EMULATED),
                    Map.entry(TransportCapabilities.PUBLISH_SUBSCRIBE, TransportCapabilitySupport.EMULATED),
                    Map.entry(TransportCapabilities.DURABILITY, TransportCapabilitySupport.UNSUPPORTED),
                    Map.entry(TransportCapabilities.COMPETING_CONSUMERS, TransportCapabilitySupport.UNSUPPORTED),
                    Map.entry(TransportCapabilities.ACKNOWLEDGEMENT, TransportCapabilitySupport.UNSUPPORTED),
                    Map.entry(TransportCapabilities.REQUEST_RESPONSE, TransportCapabilitySupport.EMULATED),
                    Map.entry(TransportCapabilities.SCHEDULING, TransportCapabilitySupport.UNSUPPORTED),
                    Map.entry(TransportCapabilities.REDELIVERY, TransportCapabilitySupport.EMULATED),
                    Map.entry(TransportCapabilities.ERROR_DESTINATIONS, TransportCapabilitySupport.EMULATED),
                    Map.entry(TransportCapabilities.ORDERING, TransportCapabilitySupport.EMULATED),
                    Map.entry(TransportCapabilities.REPLAY, TransportCapabilitySupport.UNSUPPORTED),
                    Map.entry(TransportCapabilities.TEMPORARY_ENDPOINTS, TransportCapabilitySupport.EMULATED),
                    Map.entry(TransportCapabilities.TOPOLOGY_PROVISIONING, TransportCapabilitySupport.UNSUPPORTED)));

    private TransportCapabilityDescriptors() {
    }

    public static TransportCapabilityDescriptor unknown(String transport) {
        return new TransportCapabilityDescriptor(transport, Map.of());
    }
}
