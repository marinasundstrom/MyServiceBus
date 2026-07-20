package com.myservicebus.testapp.dashboard;

import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicLong;

public final class DashboardState {
    private volatile Instant startedAtUtc;

    private final AtomicLong published = new AtomicLong();
    private final AtomicLong sent = new AtomicLong();
    private final AtomicLong consumed = new AtomicLong();
    private final AtomicLong faulted = new AtomicLong();
    private final AtomicLong consumeDurationCount = new AtomicLong();
    private final AtomicLong consumeDurationTotalMs = new AtomicLong();

    private final ConcurrentHashMap<String, QueueMetrics> queues = new ConcurrentHashMap<>();
    private final ConcurrentHashMap<String, MessageMetrics> messageTypes = new ConcurrentHashMap<>();

    public void markStarted(Instant startedAtUtc) {
        this.startedAtUtc = startedAtUtc;
    }

    public boolean isStarted() {
        return startedAtUtc != null;
    }

    public Instant getStartedAtUtc() {
        return startedAtUtc;
    }

    public void recordPublished(String messageType, String messageUrn) {
        published.incrementAndGet();
        getMessageMetrics(messageType, messageUrn).published.incrementAndGet();
    }

    public void recordSent(String messageType, String messageUrn) {
        sent.incrementAndGet();
        getMessageMetrics(messageType, messageUrn).sent.incrementAndGet();
    }

    public ConsumeScope trackConsume(String queueName, String messageType, String messageUrn) {
        QueueMetrics queueMetrics = queues.computeIfAbsent(queueName, QueueMetrics::new);
        MessageMetrics messageMetrics = getMessageMetrics(messageType, messageUrn);
        queueMetrics.inFlight.incrementAndGet();
        return new ConsumeScope(queueMetrics, messageMetrics);
    }

    public DashboardMetricsSnapshot createMetricsSnapshot(Instant generatedAtUtc) {
        Instant startedAt = startedAtUtc;
        double uptimeSeconds = startedAt == null ? 0.0 : Math.max(Duration.between(startedAt, generatedAtUtc).toMillis() / 1000.0, 0.0);
        long totalDurationCount = consumeDurationCount.get();

        return new DashboardMetricsSnapshot(
                new DashboardSnapshotFactory.DashboardCounterSetDto(
                        published.get(),
                        sent.get(),
                        consumed.get(),
                        faulted.get()),
                new DashboardSnapshotFactory.DashboardRateSetDto(
                        calculateRate(published.get(), uptimeSeconds),
                        calculateRate(sent.get(), uptimeSeconds),
                        calculateRate(consumed.get(), uptimeSeconds)),
                new DashboardSnapshotFactory.DashboardLatencySetDto(
                        totalDurationCount == 0 ? null : (double) consumeDurationTotalMs.get() / totalDurationCount),
                queues.values().stream()
                        .sorted((a, b) -> a.queueName.compareTo(b.queueName))
                        .map(x -> new DashboardSnapshotFactory.DashboardQueueMetricsDto(
                                x.queueName,
                                x.consumed.get(),
                                x.faulted.get(),
                                x.inFlight.get()))
                        .toList(),
                messageTypes.values().stream()
                        .sorted((a, b) -> a.messageType.compareTo(b.messageType))
                        .map(x -> new DashboardSnapshotFactory.DashboardMessageMetricsDto(
                                x.messageType,
                                x.messageUrn,
                                x.published.get(),
                                x.sent.get(),
                                x.consumed.get(),
                                x.faulted.get()))
                        .toList());
    }

    private static double calculateRate(long count, double uptimeSeconds) {
        return uptimeSeconds <= 0 ? 0.0 : count / uptimeSeconds;
    }

    private MessageMetrics getMessageMetrics(String messageType, String messageUrn) {
        String resolvedType = messageType == null || messageType.isBlank() ? "unknown" : messageType;
        String resolvedUrn = messageUrn == null || messageUrn.isBlank() ? "unknown" : messageUrn;
        return messageTypes.computeIfAbsent(resolvedType + "|" + resolvedUrn, ignored -> new MessageMetrics(resolvedType, resolvedUrn));
    }

    public final class ConsumeScope implements AutoCloseable {
        private final QueueMetrics queueMetrics;
        private final MessageMetrics messageMetrics;
        private final Instant startedAt = Instant.now();
        private boolean completed;

        private ConsumeScope(QueueMetrics queueMetrics, MessageMetrics messageMetrics) {
            this.queueMetrics = queueMetrics;
            this.messageMetrics = messageMetrics;
        }

        public void markSuccess() {
            if (completed) {
                return;
            }

            completed = true;
            long durationMs = Math.max(Duration.between(startedAt, Instant.now()).toMillis(), 0L);
            consumed.incrementAndGet();
            consumeDurationCount.incrementAndGet();
            consumeDurationTotalMs.addAndGet(durationMs);
            queueMetrics.consumed.incrementAndGet();
            queueMetrics.inFlight.decrementAndGet();
            messageMetrics.consumed.incrementAndGet();
        }

        public void markFault() {
            if (completed) {
                return;
            }

            completed = true;
            long durationMs = Math.max(Duration.between(startedAt, Instant.now()).toMillis(), 0L);
            faulted.incrementAndGet();
            consumeDurationCount.incrementAndGet();
            consumeDurationTotalMs.addAndGet(durationMs);
            queueMetrics.faulted.incrementAndGet();
            queueMetrics.inFlight.decrementAndGet();
            messageMetrics.faulted.incrementAndGet();
        }

        @Override
        public void close() {
            if (!completed) {
                completed = true;
                queueMetrics.inFlight.decrementAndGet();
            }
        }
    }

    private static final class QueueMetrics {
        private final String queueName;
        private final AtomicLong consumed = new AtomicLong();
        private final AtomicLong faulted = new AtomicLong();
        private final AtomicLong inFlight = new AtomicLong();

        private QueueMetrics(String queueName) {
            this.queueName = queueName;
        }
    }

    private static final class MessageMetrics {
        private final String messageType;
        private final String messageUrn;
        private final AtomicLong published = new AtomicLong();
        private final AtomicLong sent = new AtomicLong();
        private final AtomicLong consumed = new AtomicLong();
        private final AtomicLong faulted = new AtomicLong();

        private MessageMetrics(String messageType, String messageUrn) {
            this.messageType = messageType;
            this.messageUrn = messageUrn;
        }
    }

    public record DashboardMetricsSnapshot(
            DashboardSnapshotFactory.DashboardCounterSetDto totals,
            DashboardSnapshotFactory.DashboardRateSetDto rates,
            DashboardSnapshotFactory.DashboardLatencySetDto latency,
            List<DashboardSnapshotFactory.DashboardQueueMetricsDto> queues,
            List<DashboardSnapshotFactory.DashboardMessageMetricsDto> messages) {
    }
}
