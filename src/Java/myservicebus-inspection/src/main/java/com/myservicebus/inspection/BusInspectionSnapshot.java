package com.myservicebus.inspection;

import com.myservicebus.choreography.ChoreographyFragment;
import com.myservicebus.topology.SagaStateMachineTopology;
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
        List<ConsumerInspection> consumers,
        List<ChoreographyFragment> choreographies,
        List<SagaStateMachineTopology> sagaStateMachines) {

    public BusInspectionSnapshot {
        messages = List.copyOf(messages);
        receiveEndpoints = List.copyOf(receiveEndpoints);
        consumers = List.copyOf(consumers);
        choreographies = choreographies == null ? List.of() : List.copyOf(choreographies);
        sagaStateMachines = sagaStateMachines == null ? List.of() : List.copyOf(sagaStateMachines);
    }

    public BusInspectionSnapshot(
            String transportName,
            URI address,
            Instant capturedAt,
            List<MessageInspection> messages,
            List<ReceiveEndpointInspection> receiveEndpoints,
            List<ConsumerInspection> consumers) {
        this(transportName, address, capturedAt, messages, receiveEndpoints, consumers, List.of(), List.of());
    }

    public BusInspectionSnapshot(
            String transportName,
            URI address,
            Instant capturedAt,
            List<MessageInspection> messages,
            List<ReceiveEndpointInspection> receiveEndpoints,
            List<ConsumerInspection> consumers,
            List<ChoreographyFragment> choreographies) {
        this(transportName, address, capturedAt, messages, receiveEndpoints, consumers, choreographies, List.of());
    }

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
