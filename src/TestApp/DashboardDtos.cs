using System;
using System.Collections.Generic;

namespace TestApp;

public sealed record DashboardOverviewDto(
    string ServiceName,
    string TransportName,
    string Address,
    DateTime GeneratedAtUtc,
    int MessageCount,
    int ConsumerCount,
    int QueueCount,
    DashboardHealthLinksDto Health);

public sealed record DashboardMessagesDto(
    string ServiceName,
    string TransportName,
    string Address,
    DateTime GeneratedAtUtc,
    IReadOnlyList<DashboardMessageDto> Messages);

public sealed record DashboardConsumersDto(
    string ServiceName,
    string TransportName,
    string Address,
    DateTime GeneratedAtUtc,
    IReadOnlyList<DashboardConsumerDto> Consumers);

public sealed record DashboardTopologyDto(
    string ServiceName,
    string TransportName,
    string Address,
    DateTime GeneratedAtUtc,
    IReadOnlyList<DashboardMessageDto> Messages,
    IReadOnlyList<DashboardConsumerDto> Consumers);

public sealed record DashboardQueuesDto(
    string ServiceName,
    string TransportName,
    string Address,
    DateTime GeneratedAtUtc,
    IReadOnlyList<DashboardQueueDto> Queues);

public sealed record DashboardMetricsDto(
    string ServiceName,
    string TransportName,
    string Address,
    DateTime GeneratedAtUtc,
    DashboardCounterSetDto Totals,
    DashboardRateSetDto Rates,
    DashboardLatencySetDto Latency,
    IReadOnlyList<DashboardQueueMetricsDto> ByQueue,
    IReadOnlyList<DashboardMessageMetricsDto> ByMessageType);

public sealed record DashboardMessageDto(
    string MessageType,
    string MessageUrn,
    string EntityName,
    IReadOnlyList<string> ImplementedMessageTypes);

public sealed record DashboardConsumerDto(
    string ConsumerType,
    string QueueName,
    int? PrefetchCount,
    string? SerializerType,
    IReadOnlyDictionary<string, object?> QueueArguments,
    IReadOnlyList<DashboardBindingDto> Bindings);

public sealed record DashboardBindingDto(
    string MessageType,
    string MessageUrn,
    string EntityName);

public sealed record DashboardQueueDto(
    string QueueName,
    bool Durable,
    bool AutoDelete,
    int? PrefetchCount,
    IReadOnlyDictionary<string, object?> QueueArguments,
    IReadOnlyList<string> Consumers,
    IReadOnlyList<DashboardBindingDto> Bindings,
    string ErrorQueueName,
    string FaultQueueName,
    string SkippedQueueName);

public sealed record DashboardHealthLinksDto(
    string ReadyUrl,
    string LiveUrl,
    string? Status);

public sealed record DashboardCounterSetDto(
    long Published,
    long Sent,
    long Consumed,
    long Faulted);

public sealed record DashboardRateSetDto(
    double PublishedPerSecond,
    double SentPerSecond,
    double ConsumedPerSecond);

public sealed record DashboardLatencySetDto(
    double? ConsumeAvgMs);

public sealed record DashboardQueueMetricsDto(
    string QueueName,
    long Consumed,
    long Faulted,
    long InFlight);

public sealed record DashboardMessageMetricsDto(
    string MessageType,
    string MessageUrn,
    long Published,
    long Sent,
    long Consumed,
    long Faulted);
