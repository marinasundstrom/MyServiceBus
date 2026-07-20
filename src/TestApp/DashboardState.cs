using System.Collections.Concurrent;
using System.Diagnostics;

namespace TestApp;

public sealed class DashboardState
{
    private DateTime? _startedAtUtc;

    private long _published;
    private long _sent;
    private long _consumed;
    private long _faulted;
    private long _consumeDurationCount;
    private long _consumeDurationTotalMs;

    private readonly ConcurrentDictionary<string, QueueMetrics> _queues = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, MessageMetrics> _messageTypes = new(StringComparer.Ordinal);

    public void MarkStarted(DateTime startedAtUtc)
    {
        _startedAtUtc = startedAtUtc;
    }

    public bool IsStarted => _startedAtUtc.HasValue;

    public DateTime? StartedAtUtc => _startedAtUtc;

    public void RecordPublished(string? messageType, string? messageUrn)
    {
        Interlocked.Increment(ref _published);
        GetMessageMetrics(messageType, messageUrn).PublishedIncrement();
    }

    public void RecordSent(string? messageType, string? messageUrn)
    {
        Interlocked.Increment(ref _sent);
        GetMessageMetrics(messageType, messageUrn).SentIncrement();
    }

    public IDashboardConsumeScope TrackConsume(string queueName, string messageType, string messageUrn)
    {
        var queueMetrics = _queues.GetOrAdd(queueName, _ => new QueueMetrics(queueName));
        var messageMetrics = GetMessageMetrics(messageType, messageUrn);
        queueMetrics.InFlightIncrement();

        return new ConsumeScope(this, queueMetrics, messageMetrics);
    }

    public DashboardMetricsSnapshot CreateMetricsSnapshot(DateTime generatedAtUtc)
    {
        var startedAtUtc = _startedAtUtc;
        var uptimeSeconds = startedAtUtc.HasValue
            ? Math.Max((generatedAtUtc - startedAtUtc.Value).TotalSeconds, 0.0)
            : 0.0;

        var totalConsumed = Interlocked.Read(ref _consumed);
        var totalDurationCount = Interlocked.Read(ref _consumeDurationCount);
        var totalDurationMs = Interlocked.Read(ref _consumeDurationTotalMs);

        return new DashboardMetricsSnapshot(
            Totals: new DashboardCounterSetDto(
                Published: Interlocked.Read(ref _published),
                Sent: Interlocked.Read(ref _sent),
                Consumed: totalConsumed,
                Faulted: Interlocked.Read(ref _faulted)),
            Rates: new DashboardRateSetDto(
                PublishedPerSecond: CalculateRate(Interlocked.Read(ref _published), uptimeSeconds),
                SentPerSecond: CalculateRate(Interlocked.Read(ref _sent), uptimeSeconds),
                ConsumedPerSecond: CalculateRate(totalConsumed, uptimeSeconds)),
            Latency: new DashboardLatencySetDto(
                ConsumeAvgMs: totalDurationCount == 0 ? null : (double)totalDurationMs / totalDurationCount),
            Queues: _queues.Values
                .OrderBy(x => x.QueueName, StringComparer.Ordinal)
                .Select(x => new DashboardQueueMetricsDto(
                    x.QueueName,
                    x.Consumed,
                    x.Faulted,
                    x.InFlight))
                .ToArray(),
            Messages: _messageTypes.Values
                .OrderBy(x => x.MessageType, StringComparer.Ordinal)
                .Select(x => new DashboardMessageMetricsDto(
                    x.MessageType,
                    x.MessageUrn,
                    x.Published,
                    x.Sent,
                    x.Consumed,
                    x.Faulted))
                .ToArray());
    }

    private static double CalculateRate(long count, double uptimeSeconds)
        => uptimeSeconds <= 0 ? 0 : count / uptimeSeconds;

    private MessageMetrics GetMessageMetrics(string? messageType, string? messageUrn)
    {
        var resolvedType = string.IsNullOrWhiteSpace(messageType) ? "unknown" : messageType;
        var resolvedUrn = string.IsNullOrWhiteSpace(messageUrn) ? "unknown" : messageUrn;
        return _messageTypes.GetOrAdd($"{resolvedType}|{resolvedUrn}", _ => new MessageMetrics(resolvedType, resolvedUrn));
    }

    private sealed class ConsumeScope : IDashboardConsumeScope
    {
        private readonly DashboardState _state;
        private readonly QueueMetrics _queueMetrics;
        private readonly MessageMetrics _messageMetrics;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private int _completed;

        public ConsumeScope(DashboardState state, QueueMetrics queueMetrics, MessageMetrics messageMetrics)
        {
            _state = state;
            _queueMetrics = queueMetrics;
            _messageMetrics = messageMetrics;
        }

        public void MarkSuccess()
        {
            if (Interlocked.Exchange(ref _completed, 1) != 0)
                return;

            _stopwatch.Stop();
            Interlocked.Increment(ref _state._consumed);
            Interlocked.Increment(ref _state._consumeDurationCount);
            Interlocked.Add(ref _state._consumeDurationTotalMs, _stopwatch.ElapsedMilliseconds);
            _queueMetrics.ConsumedIncrement();
            _messageMetrics.ConsumedIncrement();
            _queueMetrics.InFlightDecrement();
        }

        public void MarkFault()
        {
            if (Interlocked.Exchange(ref _completed, 1) != 0)
                return;

            _stopwatch.Stop();
            Interlocked.Increment(ref _state._faulted);
            Interlocked.Increment(ref _state._consumeDurationCount);
            Interlocked.Add(ref _state._consumeDurationTotalMs, _stopwatch.ElapsedMilliseconds);
            _queueMetrics.FaultedIncrement();
            _messageMetrics.FaultedIncrement();
            _queueMetrics.InFlightDecrement();
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
            {
                _stopwatch.Stop();
                _queueMetrics.InFlightDecrement();
            }
        }
    }

    private sealed class QueueMetrics
    {
        private long _consumed;
        private long _faulted;
        private long _inFlight;

        public QueueMetrics(string queueName)
        {
            QueueName = queueName;
        }

        public string QueueName { get; }
        public long Consumed => Interlocked.Read(ref _consumed);
        public long Faulted => Interlocked.Read(ref _faulted);
        public long InFlight => Interlocked.Read(ref _inFlight);

        public void ConsumedIncrement() => Interlocked.Increment(ref _consumed);
        public void FaultedIncrement() => Interlocked.Increment(ref _faulted);
        public void InFlightIncrement() => Interlocked.Increment(ref _inFlight);
        public void InFlightDecrement() => Interlocked.Decrement(ref _inFlight);
    }

    private sealed class MessageMetrics
    {
        private long _published;
        private long _sent;
        private long _consumed;
        private long _faulted;

        public MessageMetrics(string messageType, string messageUrn)
        {
            MessageType = messageType;
            MessageUrn = messageUrn;
        }

        public string MessageType { get; }
        public string MessageUrn { get; }
        public long Published => Interlocked.Read(ref _published);
        public long Sent => Interlocked.Read(ref _sent);
        public long Consumed => Interlocked.Read(ref _consumed);
        public long Faulted => Interlocked.Read(ref _faulted);

        public void PublishedIncrement() => Interlocked.Increment(ref _published);
        public void SentIncrement() => Interlocked.Increment(ref _sent);
        public void ConsumedIncrement() => Interlocked.Increment(ref _consumed);
        public void FaultedIncrement() => Interlocked.Increment(ref _faulted);
    }
}

public interface IDashboardConsumeScope : IDisposable
{
    void MarkSuccess();
    void MarkFault();
}

public sealed record DashboardMetricsSnapshot(
    DashboardCounterSetDto Totals,
    DashboardRateSetDto Rates,
    DashboardLatencySetDto Latency,
    IReadOnlyList<DashboardQueueMetricsDto> Queues,
    IReadOnlyList<DashboardMessageMetricsDto> Messages);
