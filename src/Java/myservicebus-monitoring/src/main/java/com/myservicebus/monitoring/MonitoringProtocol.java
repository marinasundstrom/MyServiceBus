package com.myservicebus.monitoring;

import java.time.Instant;
import java.util.List;
import java.util.Map;

import com.myservicebus.inspection.BusInspectionSnapshot;

public final class MonitoringProtocol {
    public static final String VERSION = "1";

    private MonitoringProtocol() {
    }

    public record Metadata(
            String protocolVersion,
            String applicationName,
            String instanceId,
            String applicationVersion,
            String clientLanguage,
            String clientVersion,
            String busId,
            Instant startedAtUtc,
            Instant capturedAtUtc,
            BusInspectionSnapshot bus,
            Map<String, String> labels) {
    }

    public record Observation(
            long sequence,
            Instant occurredAtUtc,
            String kind,
            Boolean succeeded,
            String messageType,
            String messageUrn,
            String endpointName,
            String destinationAddress,
            Double durationMs,
            String exceptionType,
            String exceptionMessage,
            String correlationId,
            String conversationId,
            String traceId,
            String spanId,
            Integer retryAttempt,
            Integer retryLimit) {
    }

    public record ObservationBatch(
            String protocolVersion,
            String applicationName,
            String instanceId,
            String busId,
            String batchId,
            long firstSequence,
            long lastSequence,
            long droppedObservations,
            Instant exportedAtUtc,
            List<Observation> observations) {
    }

    public record Heartbeat(
            String protocolVersion,
            String applicationName,
            String instanceId,
            String busId,
            Instant sentAtUtc) {
    }
}
