package com.myservicebus.testapp.dashboard;

import com.myservicebus.MessageBus;
import com.myservicebus.MessageUrn;
import com.myservicebus.inspection.BusInspectionSnapshot;
import com.myservicebus.inspection.DefaultBusInspectionProvider;

import java.time.Instant;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public final class DashboardSnapshotFactory {
    private DashboardSnapshotFactory() {
    }

    public static DashboardOverviewDto createOverview(MessageBus bus, DashboardMetadata metadata) {
        return createOverview(new DefaultBusInspectionProvider(bus).getSnapshot(), metadata);
    }

    public static DashboardOverviewDto createOverview(MessageBus bus, DashboardMetadata metadata, DashboardState state) {
        return createOverview(new DefaultBusInspectionProvider(bus).getSnapshot(), metadata, state.isStarted() ? "Healthy" : "Unknown");
    }

    public static DashboardMessagesDto createMessages(MessageBus bus, DashboardMetadata metadata) {
        return createMessages(new DefaultBusInspectionProvider(bus).getSnapshot(), metadata);
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

    public static DashboardConsumersDto createConsumers(MessageBus bus, DashboardMetadata metadata) {
        return createConsumers(new DefaultBusInspectionProvider(bus).getSnapshot(), metadata);
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

    public static DashboardTopologyDto createTopology(MessageBus bus, DashboardMetadata metadata) {
        return createTopology(new DefaultBusInspectionProvider(bus).getSnapshot(), metadata);
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

    public static DashboardQueuesDto createQueues(MessageBus bus, DashboardMetadata metadata) {
        return createQueues(new DefaultBusInspectionProvider(bus).getSnapshot(), metadata);
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

    public static DashboardMetricsDto createMetrics(MessageBus bus, DashboardMetadata metadata, DashboardState state) {
        Instant generatedAtUtc = Instant.now();
        DashboardState.DashboardMetricsSnapshot snapshot = state.createMetricsSnapshot(generatedAtUtc);
        return new DashboardMetricsDto(
                metadata.serviceName(),
                metadata.transportName(),
                bus.getAddress().toString(),
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
