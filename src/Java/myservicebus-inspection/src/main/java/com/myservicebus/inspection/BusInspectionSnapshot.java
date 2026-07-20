package com.myservicebus.inspection;

import java.net.URI;
import java.time.Instant;
import java.util.List;
import java.util.Map;

public record BusInspectionSnapshot(
        String transportName,
        URI address,
        Instant capturedAt,
        List<MessageInspection> messages,
        List<ReceiveEndpointInspection> receiveEndpoints,
        List<ConsumerInspection> consumers) {

    public record MessageInspection(
            String messageType,
            String messageUrn,
            String entityName,
            List<String> implementedMessageTypes,
            Map<String, Object> properties) {
    }

    public record MessageBindingInspection(
            String messageType,
            String messageUrn,
            String entityName,
            Map<String, Object> properties) {
    }

    public record ConsumerInspection(
            String consumerType,
            String endpointName,
            Integer prefetchCount,
            String serializerType,
            Map<String, Object> properties) {
    }

    public record ReceiveEndpointInspection(
            String endpointName,
            String address,
            List<MessageBindingInspection> bindings,
            List<String> consumerTypes,
            TransportInspectionDetails transport,
            Map<String, Object> properties) {
    }

    public record TransportInspectionDetails(
            String transportName,
            Map<String, Object> properties) {
    }
}
