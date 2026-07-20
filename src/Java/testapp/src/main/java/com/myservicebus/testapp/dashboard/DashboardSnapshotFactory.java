package com.myservicebus.testapp.dashboard;

import com.myservicebus.MessageUrn;
import com.myservicebus.inspection.BusInspectionProvider;
import com.myservicebus.inspection.BusInspectionSnapshot;

import java.time.Instant;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public final class DashboardSnapshotFactory {
    private DashboardSnapshotFactory() {
    }

    public static DashboardOverviewDto createOverview(BusInspectionProvider inspectionProvider, DashboardMetadata metadata) {
        return createOverview(inspectionProvider.getSnapshot(), metadata);
    }

    public static DashboardOverviewDto createOverview(BusInspectionProvider inspectionProvider, DashboardMetadata metadata, DashboardState state) {
        return createOverview(inspectionProvider.getSnapshot(), metadata, state.isStarted() ? "Healthy" : "Unknown");
    }

    public static DashboardMessagesDto createMessages(BusInspectionProvider inspectionProvider, DashboardMetadata metadata) {
        return createMessages(inspectionProvider.getSnapshot(), metadata);
    }

    public static DashboardOverviewDto createOverview(BusInspectionSnapshot snapshot, DashboardMetadata metadata) {
        return new DashboardOverviewDto(
                metadata.serviceName(),
                metadata.transportName(),
                snapshot.address().toString(),
                snapshot.capturedAt(),
                snapshot.messages().size(),
                snapshot.consumers().size(),
                snapshot.receiveEndpoints().size(),
                new DashboardHealthLinksDto("/health/ready", "/health/live", null));
    }

    public static DashboardOverviewDto createOverview(BusInspectionSnapshot snapshot, DashboardMetadata metadata, String status) {
        return new DashboardOverviewDto(
                metadata.serviceName(),
                metadata.transportName(),
                snapshot.address().toString(),
                snapshot.capturedAt(),
                snapshot.messages().size(),
                snapshot.consumers().size(),
                snapshot.receiveEndpoints().size(),
                new DashboardHealthLinksDto("/health/ready", "/health/live", status));
    }

    public static DashboardMessagesDto createMessages(BusInspectionSnapshot snapshot, DashboardMetadata metadata) {
        return new DashboardMessagesDto(
                metadata.serviceName(),
                metadata.transportName(),
                snapshot.address().toString(),
                snapshot.capturedAt(),
                snapshot.messages().stream()
                        .map(x -> new DashboardMessageDto(x.messageType(), x.messageUrn(), x.entityName(), x.implementedMessageTypes()))
                        .toList());
    }

    public static DashboardConsumersDto createConsumers(BusInspectionProvider inspectionProvider, DashboardMetadata metadata) {
        return createConsumers(inspectionProvider.getSnapshot(), metadata);
    }

    public static DashboardConsumersDto createConsumers(BusInspectionSnapshot snapshot, DashboardMetadata metadata) {
        return new DashboardConsumersDto(
                metadata.serviceName(),
                metadata.transportName(),
                snapshot.address().toString(),
                snapshot.capturedAt(),
                snapshot.consumers().stream()
                        .map(x -> new DashboardConsumerDto(
                                x.consumerType(),
                                x.endpointName(),
                                x.prefetchCount(),
                                x.serializerType(),
                                x.properties(),
                                snapshot.receiveEndpoints().stream()
                                        .filter(e -> e.endpointName().equals(x.endpointName()))
                                        .findFirst()
                                        .map(e -> e.bindings().stream()
                                                .map(b -> new DashboardBindingDto(b.messageType(), b.messageUrn(), b.entityName()))
                                                .toList())
                                        .orElse(List.of())))
                        .toList());
    }

    public static DashboardTopologyDto createTopology(BusInspectionProvider inspectionProvider, DashboardMetadata metadata) {
        return createTopology(inspectionProvider.getSnapshot(), metadata);
    }

    public static DashboardTopologyDto createTopology(BusInspectionSnapshot snapshot, DashboardMetadata metadata) {
        return new DashboardTopologyDto(
                metadata.serviceName(),
                metadata.transportName(),
                snapshot.address().toString(),
                snapshot.capturedAt(),
                createMessages(snapshot, metadata).messages(),
                createConsumers(snapshot, metadata).consumers());
    }

    public static DashboardQueuesDto createQueues(BusInspectionProvider inspectionProvider, DashboardMetadata metadata) {
        return createQueues(inspectionProvider.getSnapshot(), metadata);
    }

    public static DashboardQueuesDto createQueues(BusInspectionSnapshot snapshot, DashboardMetadata metadata) {
        return new DashboardQueuesDto(
                metadata.serviceName(),
                metadata.transportName(),
                snapshot.address().toString(),
                snapshot.capturedAt(),
                snapshot.receiveEndpoints().stream()
                        .map(x -> new DashboardQueueDto(
                                x.endpointName(),
                                x.transport() != null && Boolean.TRUE.equals(x.transport().properties().get("durable")),
                                x.transport() != null && Boolean.TRUE.equals(x.transport().properties().get("autoDelete")),
                                snapshot.consumers().stream()
                                        .filter(c -> c.endpointName().equals(x.endpointName()))
                                        .findFirst()
                                        .map(BusInspectionSnapshot.ConsumerInspection::prefetchCount)
                                        .orElse(null),
                                x.properties(),
                                x.consumerTypes(),
                                x.bindings().stream()
                                        .map(b -> new DashboardBindingDto(b.messageType(), b.messageUrn(), b.entityName()))
                                        .toList(),
                                x.transport() != null && x.transport().properties().containsKey("errorQueueName") ? x.transport().properties().get("errorQueueName").toString() : x.endpointName() + "_error",
                                x.transport() != null && x.transport().properties().containsKey("faultQueueName") ? x.transport().properties().get("faultQueueName").toString() : x.endpointName() + "_fault",
                                x.transport() != null && x.transport().properties().containsKey("skippedQueueName") ? x.transport().properties().get("skippedQueueName").toString() : x.endpointName() + "_skipped"))
                        .toList());
    }

    public static DashboardMetricsDto createMetrics(BusInspectionProvider inspectionProvider, DashboardMetadata metadata, DashboardState state) {
        Instant generatedAtUtc = Instant.now();
        BusInspectionSnapshot inspectionSnapshot = inspectionProvider.getSnapshot();
        DashboardState.DashboardMetricsSnapshot snapshot = state.createMetricsSnapshot(generatedAtUtc);
        return new DashboardMetricsDto(
                metadata.serviceName(),
                metadata.transportName(),
                inspectionSnapshot.address().toString(),
                generatedAtUtc,
                snapshot.totals(),
                snapshot.rates(),
                snapshot.latency(),
                snapshot.queues(),
                snapshot.messages());
    }

    public static String resolveMessageTypeFromDestination(java.net.URI destinationAddress) {
        String path = destinationAddress.getPath();
        if (path == null || path.isBlank()) {
            return null;
        }

        String[] segments = path.replaceAll("^/+", "").split("/");
        if (segments.length == 0) {
            return null;
        }

        String finalSegment = segments[segments.length - 1];
        return finalSegment.isBlank() ? null : finalSegment;
    }

    public static String resolveMessageUrnFromDestination(java.net.URI destinationAddress) {
        String messageType = resolveMessageTypeFromDestination(destinationAddress);
        return messageType == null ? null : "destination:" + messageType;
    }

    public record DashboardOverviewDto(
            String serviceName,
            String transportName,
            String address,
            Instant generatedAtUtc,
            int messageCount,
            int consumerCount,
            int queueCount,
            DashboardHealthLinksDto health) {
    }

    public record DashboardMessagesDto(
            String serviceName,
            String transportName,
            String address,
            Instant generatedAtUtc,
            List<DashboardMessageDto> messages) {
    }

    public record DashboardConsumersDto(
            String serviceName,
            String transportName,
            String address,
            Instant generatedAtUtc,
            List<DashboardConsumerDto> consumers) {
    }

    public record DashboardTopologyDto(
            String serviceName,
            String transportName,
            String address,
            Instant generatedAtUtc,
            List<DashboardMessageDto> messages,
            List<DashboardConsumerDto> consumers) {
    }

    public record DashboardQueuesDto(
            String serviceName,
            String transportName,
            String address,
            Instant generatedAtUtc,
            List<DashboardQueueDto> queues) {
    }

    public record DashboardMetricsDto(
            String serviceName,
            String transportName,
            String address,
            Instant generatedAtUtc,
            DashboardCounterSetDto totals,
            DashboardRateSetDto rates,
            DashboardLatencySetDto latency,
            List<DashboardQueueMetricsDto> byQueue,
            List<DashboardMessageMetricsDto> byMessageType) {
    }

    public record DashboardMessageDto(
            String messageType,
            String messageUrn,
            String entityName,
            List<String> implementedMessageTypes) {
    }

    public record DashboardConsumerDto(
            String consumerType,
            String queueName,
            Integer prefetchCount,
            String serializerType,
            Map<String, Object> queueArguments,
            List<DashboardBindingDto> bindings) {
    }

    public record DashboardBindingDto(
            String messageType,
            String messageUrn,
            String entityName) {
    }

    public record DashboardQueueDto(
            String queueName,
            boolean durable,
            boolean autoDelete,
            Integer prefetchCount,
            Map<String, Object> queueArguments,
            List<String> consumers,
            List<DashboardBindingDto> bindings,
            String errorQueueName,
            String faultQueueName,
            String skippedQueueName) {
    }

    public record DashboardHealthLinksDto(
            String readyUrl,
            String liveUrl,
            String status) {
    }

    public record DashboardCounterSetDto(
            long published,
            long sent,
            long consumed,
            long faulted) {
    }

    public record DashboardRateSetDto(
            double publishedPerSecond,
            double sentPerSecond,
            double consumedPerSecond) {
    }

    public record DashboardLatencySetDto(
            Double consumeAvgMs) {
    }

    public record DashboardQueueMetricsDto(
            String queueName,
            long consumed,
            long faulted,
            long inFlight) {
    }

    public record DashboardMessageMetricsDto(
            String messageType,
            String messageUrn,
            long published,
            long sent,
            long consumed,
            long faulted) {
    }
}
