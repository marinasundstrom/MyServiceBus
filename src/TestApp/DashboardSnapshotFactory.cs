using System;
using System.Collections.Generic;
using System.Linq;
using MyServiceBus;
using MyServiceBus.Inspection;

namespace TestApp;

public static class DashboardSnapshotFactory
{
    public static DashboardOverviewDto CreateOverview(IMessageBus bus, DashboardMetadata metadata)
        => CreateOverview(new BusInspectionProvider(bus).GetSnapshot(), metadata);

    public static DashboardOverviewDto CreateOverview(IMessageBus bus, DashboardMetadata metadata, DashboardState state)
        => CreateOverview(new BusInspectionProvider(bus).GetSnapshot(), metadata, state.IsStarted ? "Healthy" : "Unknown");

    public static DashboardOverviewDto CreateOverview(BusInspectionSnapshot snapshot, DashboardMetadata metadata)
        => new(
            metadata.ServiceName,
            metadata.TransportName,
            snapshot.Address.ToString(),
            snapshot.CapturedAt.UtcDateTime,
            snapshot.Messages.Count,
            snapshot.Consumers.Count,
            snapshot.ReceiveEndpoints.Count,
            new DashboardHealthLinksDto("/health/ready", "/health/live", null));

    public static DashboardOverviewDto CreateOverview(BusInspectionSnapshot snapshot, DashboardMetadata metadata, string? status)
        => new(
            metadata.ServiceName,
            metadata.TransportName,
            snapshot.Address.ToString(),
            snapshot.CapturedAt.UtcDateTime,
            snapshot.Messages.Count,
            snapshot.Consumers.Count,
            snapshot.ReceiveEndpoints.Count,
            new DashboardHealthLinksDto("/health/ready", "/health/live", status));

    public static DashboardMessagesDto CreateMessages(IMessageBus bus, DashboardMetadata metadata)
        => CreateMessages(new BusInspectionProvider(bus).GetSnapshot(), metadata);

    public static DashboardMessagesDto CreateMessages(BusInspectionSnapshot snapshot, DashboardMetadata metadata)
        => new(
            metadata.ServiceName,
            metadata.TransportName,
            snapshot.Address.ToString(),
            snapshot.CapturedAt.UtcDateTime,
            snapshot.Messages
                .Select(x => new DashboardMessageDto(x.MessageType, x.MessageUrn, x.EntityName, x.ImplementedMessageTypes))
                .ToArray());

    public static DashboardConsumersDto CreateConsumers(IMessageBus bus, DashboardMetadata metadata)
        => CreateConsumers(new BusInspectionProvider(bus).GetSnapshot(), metadata);

    public static DashboardConsumersDto CreateConsumers(BusInspectionSnapshot snapshot, DashboardMetadata metadata)
        => new(
            metadata.ServiceName,
            metadata.TransportName,
            snapshot.Address.ToString(),
            snapshot.CapturedAt.UtcDateTime,
            snapshot.Consumers
                .Select(x => new DashboardConsumerDto(
                    x.ConsumerType,
                    x.EndpointName,
                    x.PrefetchCount,
                    x.SerializerType,
                    x.Properties,
                    snapshot.ReceiveEndpoints.First(e => e.EndpointName == x.EndpointName).Bindings
                        .Select(b => new DashboardBindingDto(b.MessageType, b.MessageUrn, b.EntityName))
                        .ToArray()))
                .ToArray());

    public static DashboardTopologyDto CreateTopology(IMessageBus bus, DashboardMetadata metadata)
        => CreateTopology(new BusInspectionProvider(bus).GetSnapshot(), metadata);

    public static DashboardTopologyDto CreateTopology(BusInspectionSnapshot snapshot, DashboardMetadata metadata)
        => new(
            metadata.ServiceName,
            metadata.TransportName,
            snapshot.Address.ToString(),
            snapshot.CapturedAt.UtcDateTime,
            CreateMessages(snapshot, metadata).Messages,
            CreateConsumers(snapshot, metadata).Consumers);

    public static DashboardQueuesDto CreateQueues(IMessageBus bus, DashboardMetadata metadata)
        => CreateQueues(new BusInspectionProvider(bus).GetSnapshot(), metadata);

    public static DashboardQueuesDto CreateQueues(BusInspectionSnapshot snapshot, DashboardMetadata metadata)
        => new(
            metadata.ServiceName,
            metadata.TransportName,
            snapshot.Address.ToString(),
            snapshot.CapturedAt.UtcDateTime,
            snapshot.ReceiveEndpoints
                .Select(x => new DashboardQueueDto(
                    x.EndpointName,
                    Durable: x.Transport?.Properties.TryGetValue("durable", out var durable) == true && durable is bool durableValue ? durableValue : true,
                    AutoDelete: x.Transport?.Properties.TryGetValue("autoDelete", out var autoDelete) == true && autoDelete is bool autoDeleteValue ? autoDeleteValue : false,
                    PrefetchCount: snapshot.Consumers.FirstOrDefault(c => c.EndpointName == x.EndpointName)?.PrefetchCount,
                    QueueArguments: x.Properties,
                    Consumers: x.ConsumerTypes,
                    Bindings: x.Bindings.Select(b => new DashboardBindingDto(b.MessageType, b.MessageUrn, b.EntityName)).ToArray(),
                    ErrorQueueName: x.Transport?.Properties.TryGetValue("errorQueueName", out var errorQueueName) == true ? errorQueueName?.ToString() ?? $"{x.EndpointName}_error" : $"{x.EndpointName}_error",
                    FaultQueueName: x.Transport?.Properties.TryGetValue("faultQueueName", out var faultQueueName) == true ? faultQueueName?.ToString() ?? $"{x.EndpointName}_fault" : $"{x.EndpointName}_fault",
                    SkippedQueueName: x.Transport?.Properties.TryGetValue("skippedQueueName", out var skippedQueueName) == true ? skippedQueueName?.ToString() ?? $"{x.EndpointName}_skipped" : $"{x.EndpointName}_skipped"))
                .ToArray());

    public static DashboardMetricsDto CreateMetrics(IMessageBus bus, DashboardMetadata metadata, DashboardState state)
    {
        var generatedAtUtc = DateTime.UtcNow;
        var snapshot = state.CreateMetricsSnapshot(generatedAtUtc);
        return new DashboardMetricsDto(
            metadata.ServiceName,
            metadata.TransportName,
            bus.Address.ToString(),
            generatedAtUtc,
            snapshot.Totals,
            snapshot.Rates,
            snapshot.Latency,
            snapshot.Queues,
            snapshot.Messages);
    }

    public static string? ResolveMessageTypeFromDestination(Uri destinationAddress)
    {
        var segments = destinationAddress.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return null;

        var finalSegment = segments[^1];
        return string.IsNullOrWhiteSpace(finalSegment) ? null : finalSegment;
    }

    public static string? ResolveMessageUrnFromDestination(Uri destinationAddress)
    {
        var messageType = ResolveMessageTypeFromDestination(destinationAddress);
        return messageType is null ? null : $"destination:{messageType}";
    }
}
