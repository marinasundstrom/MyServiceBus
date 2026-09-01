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
            Integer retryLimit,
            Map<String, String> properties,
            String messageId,
            String causationMessageId) {
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

    public record ScheduledWorkSnapshot(
            String protocolVersion,
            String applicationName,
            String instanceId,
            String busId,
            Instant capturedAtUtc,
            List<ScheduledWorkItem> items) {
    }

    public record ScheduledWorkItem(
            String tokenId,
            String provider,
            String durability,
            String workKind,
            String messageType,
            String intent,
            String destinationAddress,
            Instant dueAtUtc,
            String status,
            String providerStatus,
            int attempt,
            Instant updatedAtUtc,
            String failureCategory) {
    }

    public record RecurringJobSnapshot(
            String protocolVersion,
            String applicationName,
            String instanceId,
            String busId,
            Instant capturedAtUtc,
            List<RecurringJobItem> items) {
    }

    public record RecurringJobItem(
            String definitionId,
            String scheduleId,
            String scheduleGroup,
            long revision,
            String provider,
            String durability,
            String placement,
            String cadence,
            String messageType,
            String status,
            Instant nextOccurrenceAtUtc,
            Instant updatedAtUtc) {
    }

    public record JobSnapshot(
            String protocolVersion,
            String applicationName,
            String instanceId,
            String busId,
            Instant capturedAtUtc,
            List<JobItem> items) {
    }

    public record JobItem(
            String jobId,
            String jobType,
            String status,
            String provider,
            String durability,
            String placement,
            Instant submittedAtUtc,
            Instant scheduledForUtc,
            Instant startedAtUtc,
            Instant completedAtUtc,
            Long progressValue,
            Long progressLimit,
            String recurringJobOccurrenceId,
            Instant updatedAtUtc,
            List<JobAttemptItem> attempts) {
    }

    public record JobAttemptItem(
            String attemptId,
            int retryAttempt,
            String status,
            Instant startedAtUtc,
            Instant completedAtUtc,
            String failureCategory) {
    }
}
