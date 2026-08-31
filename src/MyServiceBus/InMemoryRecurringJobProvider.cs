namespace MyServiceBus;

public sealed class InMemoryRecurringJobProvider : IRecurringJobProvider, IRecurringJobSource
{
    private sealed class Entry
    {
        public required Guid DefinitionId { get; init; }
        public required RecurringJobDefinition Definition { get; set; }
        public required object Job { get; set; }
        public required Func<CancellationToken, Task> Dispatch { get; set; }
        public required long Revision { get; set; }
        public required DateTimeOffset AcceptedAtUtc { get; set; }
        public required RecurringJobDefinitionStatus Status { get; set; }
        public DateTimeOffset? NextOccurrenceAtUtc { get; set; }
        public Guid? TimerToken { get; set; }
    }

    private readonly object gate = new();
    private readonly Dictionary<RecurringJobIdentity, Entry> definitions = [];
    private readonly HashSet<(Guid DefinitionId, long Revision, DateTimeOffset ScheduledForUtc)> occurrences = [];
    private readonly IPublishEndpoint publishEndpoint;
    private readonly ILocalDelayScheduler delayScheduler;
    private readonly TimeProvider timeProvider;

    public InMemoryRecurringJobProvider(
        IPublishEndpoint publishEndpoint,
        ILocalDelayScheduler delayScheduler,
        TimeProvider? timeProvider = null)
    {
        this.publishEndpoint = publishEndpoint;
        this.delayScheduler = delayScheduler;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string ProviderName => "InMemory";

    public SchedulingDurability Durability => SchedulingDurability.Volatile;

    public SchedulingPlacement Placement => SchedulingPlacement.ProcessLocal;

    string IRecurringJobSource.Provider => ProviderName;

    bool IRecurringJobSource.Authoritative => true;

    public Task<IReadOnlyList<RecurringJobState>> GetSnapshotAsync(
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            IReadOnlyList<RecurringJobState> snapshot = definitions.Values
                .Where(entry => entry.Status != RecurringJobDefinitionStatus.Removed)
                .OrderBy(entry => entry.NextOccurrenceAtUtc ?? DateTimeOffset.MaxValue)
                .Take(maximumCount)
                .Select(entry => new RecurringJobState(
                    entry.DefinitionId,
                    entry.Definition.Identity,
                    entry.Revision,
                    ProviderName,
                    Durability,
                    Placement,
                    FormatCadence(entry.Definition.Cadence),
                    entry.Job.GetType().FullName ?? entry.Job.GetType().Name,
                    entry.Status,
                    entry.NextOccurrenceAtUtc,
                    entry.AcceptedAtUtc))
                .ToArray();
            return Task.FromResult(snapshot);
        }
    }

    public async Task<RecurringJobDefinitionReceipt> AddOrUpdate<TJob>(
        RecurringJobDefinition definition,
        TJob job,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default)
        where TJob : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(job);
        EnsureSupported(definition);

        Entry entry;
        Guid? timerToCancel;
        lock (gate)
        {
            definitions.TryGetValue(definition.Identity, out var current);
            ValidateExpectedRevision(definition.Identity, expectedRevision, current?.Revision ?? 0);

            if (current is not null
                && current.Status != RecurringJobDefinitionStatus.Removed
                && current.Definition == definition
                && Equals(current.Job, job))
            {
                return CreateReceipt(current);
            }

            timerToCancel = current?.TimerToken;
            entry = new Entry
            {
                DefinitionId = current?.DefinitionId ?? Guid.NewGuid(),
                Definition = definition,
                Job = job,
                Dispatch = token => publishEndpoint.Publish(job, cancellationToken: token),
                Revision = (current?.Revision ?? 0) + 1,
                AcceptedAtUtc = timeProvider.GetUtcNow(),
                Status = RecurringJobDefinitionStatus.Active
            };
            definitions[definition.Identity] = entry;
        }

        if (timerToCancel is Guid oldTimer)
            await delayScheduler.Cancel(oldTimer).ConfigureAwait(false);

        await ScheduleNext(entry, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return CreateReceipt(entry);
    }

    public async Task<RecurringJobControlResult> Pause(
        RecurringJobIdentity identity,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default)
    {
        Guid? timer;
        long revision;
        lock (gate)
        {
            if (!definitions.TryGetValue(identity, out var entry)
                || entry.Status == RecurringJobDefinitionStatus.Removed)
                return new(RecurringJobControlOutcome.NotFound);

            ValidateExpectedRevision(identity, expectedRevision, entry.Revision);
            if (entry.Status == RecurringJobDefinitionStatus.Paused)
                return new(RecurringJobControlOutcome.Unchanged, entry.Revision);

            entry.Status = RecurringJobDefinitionStatus.Paused;
            entry.Revision++;
            entry.NextOccurrenceAtUtc = null;
            timer = entry.TimerToken;
            entry.TimerToken = null;
            revision = entry.Revision;
        }

        if (timer is Guid timerToken)
            await delayScheduler.Cancel(timerToken).ConfigureAwait(false);

        return new(RecurringJobControlOutcome.Applied, revision);
    }

    public async Task<RecurringJobControlResult> Resume(
        RecurringJobIdentity identity,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default)
    {
        Entry entry;
        lock (gate)
        {
            if (!definitions.TryGetValue(identity, out entry!)
                || entry.Status == RecurringJobDefinitionStatus.Removed)
                return new(RecurringJobControlOutcome.NotFound);

            ValidateExpectedRevision(identity, expectedRevision, entry.Revision);
            if (entry.Status == RecurringJobDefinitionStatus.Active)
                return new(RecurringJobControlOutcome.Unchanged, entry.Revision);

            entry.Status = RecurringJobDefinitionStatus.Active;
            entry.Revision++;
        }

        await ScheduleNext(entry, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return new(RecurringJobControlOutcome.Applied, entry.Revision);
    }

    public async Task<RecurringJobControlResult> Remove(
        RecurringJobIdentity identity,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default)
    {
        Guid? timer;
        long revision;
        lock (gate)
        {
            if (!definitions.TryGetValue(identity, out var entry)
                || entry.Status == RecurringJobDefinitionStatus.Removed)
                return new(RecurringJobControlOutcome.NotFound);

            ValidateExpectedRevision(identity, expectedRevision, entry.Revision);
            entry.Status = RecurringJobDefinitionStatus.Removed;
            entry.Revision++;
            entry.NextOccurrenceAtUtc = null;
            timer = entry.TimerToken;
            entry.TimerToken = null;
            revision = entry.Revision;
        }

        if (timer is Guid timerToken)
            await delayScheduler.Cancel(timerToken).ConfigureAwait(false);

        return new(RecurringJobControlOutcome.Applied, revision);
    }

    public async Task<RecurringJobOccurrenceReceipt> TriggerNow(
        RecurringJobIdentity identity,
        CancellationToken cancellationToken = default)
    {
        Entry entry;
        DateTimeOffset scheduledFor;
        Guid occurrenceId;
        lock (gate)
        {
            if (!definitions.TryGetValue(identity, out entry!)
                || entry.Status is RecurringJobDefinitionStatus.Removed or RecurringJobDefinitionStatus.Ended)
                throw new RecurringJobNotFoundException(identity);

            scheduledFor = timeProvider.GetUtcNow();
            occurrenceId = Guid.NewGuid();
        }

        await entry.Dispatch(cancellationToken).ConfigureAwait(false);
        return new(
            occurrenceId,
            entry.DefinitionId,
            entry.Revision,
            scheduledFor,
            true,
            RecurringJobOccurrenceStatus.Dispatched);
    }

    private async Task ScheduleNext(Entry entry, DateTimeOffset afterUtc, CancellationToken cancellationToken)
    {
        var next = CalculateNext(entry, afterUtc);
        await ScheduleAt(entry, next, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScheduleAt(
        Entry entry,
        DateTimeOffset? next,
        CancellationToken cancellationToken)
    {
        if (next is null)
        {
            lock (gate)
            {
                if (IsCurrent(entry))
                {
                    entry.Status = RecurringJobDefinitionStatus.Ended;
                    entry.NextOccurrenceAtUtc = null;
                }
            }
            return;
        }

        var identity = entry.Definition.Identity;
        var revision = entry.Revision;
        var timerToken = await delayScheduler.Schedule(
            next.Value.UtcDateTime,
            token => Materialize(identity, revision, next.Value, token),
            cancellationToken).ConfigureAwait(false);

        var cancelTimer = false;
        lock (gate)
        {
            if (IsCurrent(entry) && entry.Status == RecurringJobDefinitionStatus.Active)
            {
                entry.NextOccurrenceAtUtc = next;
                entry.TimerToken = timerToken;
            }
            else
            {
                cancelTimer = true;
            }
        }

        if (cancelTimer)
            await delayScheduler.Cancel(timerToken).ConfigureAwait(false);
    }

    private async Task Materialize(
        RecurringJobIdentity identity,
        long revision,
        DateTimeOffset scheduledForUtc,
        CancellationToken cancellationToken)
    {
        Entry entry;
        int dispatchCount;
        DateTimeOffset? next;
        lock (gate)
        {
            if (!definitions.TryGetValue(identity, out entry!)
                || entry.Revision != revision
                || entry.Status != RecurringJobDefinitionStatus.Active
                || occurrences.Contains((entry.DefinitionId, revision, scheduledForUtc)))
                return;

            entry.TimerToken = null;
            entry.NextOccurrenceAtUtc = null;
            (dispatchCount, next) = EvaluateDue(entry, scheduledForUtc, timeProvider.GetUtcNow());
            occurrences.Add((entry.DefinitionId, revision, scheduledForUtc));
            for (var index = 1; index < dispatchCount; index++)
            {
                var occurrenceTime = scheduledForUtc + TimeSpan.FromTicks(
                    checked(((FixedIntervalRecurringJobCadence)entry.Definition.Cadence).Interval.Ticks * index));
                occurrences.Add((entry.DefinitionId, revision, occurrenceTime));
            }
        }

        await ScheduleAt(entry, next, cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < dispatchCount; index++)
            await entry.Dispatch(cancellationToken).ConfigureAwait(false);
    }

    private bool IsCurrent(Entry entry) =>
        definitions.TryGetValue(entry.Definition.Identity, out var current)
        && ReferenceEquals(current, entry);

    private static (int DispatchCount, DateTimeOffset? Next) EvaluateDue(
        Entry entry,
        DateTimeOffset scheduledForUtc,
        DateTimeOffset nowUtc)
    {
        var following = CalculateNext(entry, scheduledForUtc);
        var isMisfire = following is { } nextAfterScheduled && nextAfterScheduled <= nowUtc;
        if (!isMisfire)
            return (1, following);

        var dispatchCount = entry.Definition.MisfirePolicy switch
        {
            RecurringJobMisfirePolicy.Skip => 0,
            RecurringJobMisfirePolicy.FireOnceNow => 1,
            RecurringJobMisfirePolicy.CatchUp => CountCatchUpOccurrences(entry, scheduledForUtc, nowUtc),
            _ => throw new NotSupportedException($"Unsupported misfire policy '{entry.Definition.MisfirePolicy}'.")
        };
        return (dispatchCount, CalculateNext(entry, nowUtc));
    }

    private static int CountCatchUpOccurrences(
        Entry entry,
        DateTimeOffset scheduledForUtc,
        DateTimeOffset nowUtc)
    {
        var cadence = (FixedIntervalRecurringJobCadence)entry.Definition.Cadence;
        var lastEligible = entry.Definition.EndAtUtc is { } end && end <= nowUtc
            ? end.AddTicks(-1)
            : nowUtc;
        var elapsedIntervals = (lastEligible - scheduledForUtc).Ticks / cadence.Interval.Ticks;
        return (int)Math.Min(checked(elapsedIntervals + 1), entry.Definition.MaxCatchUpOccurrences);
    }

    private static DateTimeOffset? CalculateNext(Entry entry, DateTimeOffset afterUtc)
    {
        var definition = entry.Definition;
        var cadence = (FixedIntervalRecurringJobCadence)definition.Cadence;
        var anchor = cadence.AnchorAtUtc ?? definition.StartAtUtc ?? entry.AcceptedAtUtc;
        var threshold = definition.StartAtUtc is { } start && start > afterUtc ? start.AddTicks(-1) : afterUtc;
        DateTimeOffset next;
        if (anchor > threshold)
        {
            next = anchor;
        }
        else
        {
            var elapsedTicks = (threshold - anchor).Ticks;
            var steps = (elapsedTicks / cadence.Interval.Ticks) + 1;
            next = anchor + TimeSpan.FromTicks(checked(steps * cadence.Interval.Ticks));
        }

        return definition.EndAtUtc is { } end && next >= end ? null : next;
    }

    private static void EnsureSupported(RecurringJobDefinition definition)
    {
        if (definition.Cadence is not FixedIntervalRecurringJobCadence)
            throw new NotSupportedException("The in-memory recurring scheduler currently supports fixed intervals only.");
        if (definition.OverlapPolicy != RecurringJobOverlapPolicy.Allow)
            throw new NotSupportedException("The dispatch-only recurring scheduler supports the Allow overlap policy only.");
    }

    private static string FormatCadence(RecurringJobCadence cadence) => cadence switch
    {
        FixedIntervalRecurringJobCadence fixedInterval => $"Every {fixedInterval.Interval}",
        CronRecurringJobCadence cron => $"{cron.Dialect}: {cron.Expression} ({cron.TimeZoneId})",
        _ => cadence.GetType().Name
    };

    private static void ValidateExpectedRevision(
        RecurringJobIdentity identity,
        long? expectedRevision,
        long currentRevision)
    {
        if (expectedRevision is { } expected && expected != currentRevision)
            throw new RecurringJobRevisionConflictException(identity, expected, currentRevision);
    }

    private RecurringJobDefinitionReceipt CreateReceipt(Entry entry) => new(
        entry.DefinitionId,
        entry.Definition.Identity,
        entry.Revision,
        ProviderName,
        Durability,
        Placement,
        entry.AcceptedAtUtc,
        entry.NextOccurrenceAtUtc);
}
