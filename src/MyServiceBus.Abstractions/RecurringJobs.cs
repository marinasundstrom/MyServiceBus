namespace MyServiceBus;

public sealed record RecurringJobIdentity
{
    /// <summary>
    /// Creates the caller-owned identity of a recurring job definition.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="scheduleId"/> is blank.</exception>
    public RecurringJobIdentity(string scheduleId, string? scheduleGroup = null)
    {
        ScheduleId = RequireValue(scheduleId, nameof(scheduleId));
        ScheduleGroup = NormalizeOptional(scheduleGroup);
    }

    public string ScheduleId { get; }

    public string? ScheduleGroup { get; }

    private static string RequireValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public abstract record RecurringJobCadence;

public sealed record FixedIntervalRecurringJobCadence : RecurringJobCadence
{
    /// <summary>
    /// Creates a fixed interval cadence, optionally anchored to a specific instant.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is not positive.</exception>
    public FixedIntervalRecurringJobCadence(TimeSpan interval, DateTimeOffset? anchorAtUtc = null)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "The recurring interval must be greater than zero.");

        Interval = interval;
        AnchorAtUtc = anchorAtUtc?.ToUniversalTime();
    }

    public TimeSpan Interval { get; }

    public DateTimeOffset? AnchorAtUtc { get; }
}

public enum RecurringJobCronDialect
{
    Unix5,
    Quartz
}

public sealed record CronRecurringJobCadence : RecurringJobCadence
{
    /// <summary>
    /// Creates a cron cadence whose expression is interpreted only using the declared dialect.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The expression or time-zone identifier is blank, or the dialect is not defined.
    /// </exception>
    public CronRecurringJobCadence(
        string expression,
        RecurringJobCronDialect dialect,
        string timeZoneId = "UTC")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (!Enum.IsDefined(dialect))
            throw new ArgumentException("The cron dialect is not defined.", nameof(dialect));

        Expression = expression.Trim();
        Dialect = dialect;
        TimeZoneId = timeZoneId.Trim();
    }

    public string Expression { get; }

    public RecurringJobCronDialect Dialect { get; }

    public string TimeZoneId { get; }
}

public enum RecurringJobMisfirePolicy
{
    Skip,
    FireOnceNow,
    CatchUp
}

public enum RecurringJobOverlapPolicy
{
    Allow,
    Forbid,
    Queue
}

public sealed record RecurringJobDefinition
{
    /// <summary>
    /// Creates a validated recurring job definition. The job command is supplied separately when
    /// the definition is added or updated.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identity"/> or <paramref name="cadence"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxCatchUpOccurrences"/> is not positive.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The end time is not later than the start time, or a policy value is not defined.
    /// </exception>
    public RecurringJobDefinition(
        RecurringJobIdentity identity,
        RecurringJobCadence cadence,
        string? description = null,
        DateTimeOffset? startAtUtc = null,
        DateTimeOffset? endAtUtc = null,
        RecurringJobMisfirePolicy misfirePolicy = RecurringJobMisfirePolicy.FireOnceNow,
        int maxCatchUpOccurrences = 1,
        RecurringJobOverlapPolicy overlapPolicy = RecurringJobOverlapPolicy.Allow)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(cadence);

        if (!Enum.IsDefined(misfirePolicy))
            throw new ArgumentException("The misfire policy is not defined.", nameof(misfirePolicy));
        if (!Enum.IsDefined(overlapPolicy))
            throw new ArgumentException("The overlap policy is not defined.", nameof(overlapPolicy));

        if (maxCatchUpOccurrences <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCatchUpOccurrences), "The catch-up cap must be greater than zero.");

        var normalizedStart = startAtUtc?.ToUniversalTime();
        var normalizedEnd = endAtUtc?.ToUniversalTime();
        if (normalizedStart is not null && normalizedEnd is not null && normalizedEnd <= normalizedStart)
            throw new ArgumentException("The recurring job end time must be later than its start time.", nameof(endAtUtc));

        Identity = identity;
        Cadence = cadence;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartAtUtc = normalizedStart;
        EndAtUtc = normalizedEnd;
        MisfirePolicy = misfirePolicy;
        MaxCatchUpOccurrences = maxCatchUpOccurrences;
        OverlapPolicy = overlapPolicy;
    }

    public RecurringJobIdentity Identity { get; }

    public RecurringJobCadence Cadence { get; }

    public string? Description { get; }

    public DateTimeOffset? StartAtUtc { get; }

    public DateTimeOffset? EndAtUtc { get; }

    public RecurringJobMisfirePolicy MisfirePolicy { get; }

    public int MaxCatchUpOccurrences { get; }

    public RecurringJobOverlapPolicy OverlapPolicy { get; }
}

public enum RecurringJobDefinitionStatus
{
    Active,
    Paused,
    Ended,
    Removed
}

public enum RecurringJobOccurrenceStatus
{
    Pending,
    Acquired,
    Dispatched,
    Running,
    RetryScheduled,
    Completed,
    Cancelled,
    Skipped,
    Failed
}

public enum RecurringJobControlOutcome
{
    Applied,
    Unchanged,
    RevisionConflict,
    Unsupported,
    NotFound
}

public sealed record RecurringJobDefinitionReceipt(
    Guid DefinitionId,
    RecurringJobIdentity Identity,
    long Revision,
    string Provider,
    SchedulingDurability Durability,
    SchedulingPlacement Placement,
    DateTimeOffset AcceptedAtUtc,
    DateTimeOffset? NextOccurrenceAtUtc);

public sealed record RecurringJobOccurrenceReceipt(
    Guid OccurrenceId,
    Guid DefinitionId,
    long DefinitionRevision,
    DateTimeOffset ScheduledForUtc,
    bool IsManual,
    RecurringJobOccurrenceStatus Status);

public sealed record RecurringJobControlResult(
    RecurringJobControlOutcome Outcome,
    long? CurrentRevision = null);

public sealed class RecurringJobRevisionConflictException : Exception
{
    public RecurringJobRevisionConflictException(
        RecurringJobIdentity identity,
        long expectedRevision,
        long currentRevision)
        : base($"Recurring job '{identity.ScheduleId}' has revision {currentRevision}, not {expectedRevision}.")
    {
        Identity = identity;
        ExpectedRevision = expectedRevision;
        CurrentRevision = currentRevision;
    }

    public RecurringJobIdentity Identity { get; }

    public long ExpectedRevision { get; }

    public long CurrentRevision { get; }
}

public sealed class RecurringJobNotFoundException : Exception
{
    public RecurringJobNotFoundException(RecurringJobIdentity identity)
        : base($"Recurring job '{identity.ScheduleId}' was not found.")
    {
        Identity = identity;
    }

    public RecurringJobIdentity Identity { get; }
}

public interface IRecurringJobScheduler
{
    /// <exception cref="RecurringJobRevisionConflictException">
    /// The expected revision does not match the current definition.
    /// </exception>
    /// <exception cref="NotSupportedException">The provider does not support the requested cadence or policy.</exception>
    Task<RecurringJobDefinitionReceipt> AddOrUpdate<TJob>(
        RecurringJobDefinition definition,
        TJob job,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default)
        where TJob : class;

    Task<RecurringJobControlResult> Pause(
        RecurringJobIdentity identity,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default);

    Task<RecurringJobControlResult> Resume(
        RecurringJobIdentity identity,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default);

    Task<RecurringJobControlResult> Remove(
        RecurringJobIdentity identity,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default);

    Task<RecurringJobOccurrenceReceipt> TriggerNow(
        RecurringJobIdentity identity,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provider integration boundary for recurring definitions and occurrence materialization.
/// Applications use <see cref="IRecurringJobScheduler"/> instead.
/// </summary>
public interface IRecurringJobProvider : IRecurringJobScheduler
{
    string ProviderName { get; }

    SchedulingDurability Durability { get; }

    SchedulingPlacement Placement { get; }
}
