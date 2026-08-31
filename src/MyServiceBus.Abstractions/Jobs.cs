namespace MyServiceBus;

/// <summary>
/// Marker interface for a tracked, long-running application job consumer.
/// </summary>
public interface IJobConsumer : IConsumer
{
}

public interface IJobConsumer<in TJob> : IJobConsumer
    where TJob : class
{
    Task Run(JobContext<TJob> context);
}

public interface JobContext<out TJob>
    where TJob : class
{
    Guid JobId { get; }

    Guid AttemptId { get; }

    int RetryAttempt { get; }

    TJob Job { get; }

    TimeSpan ElapsedTime { get; }

    CancellationToken CancellationToken { get; }

    Task SetProgress(
        long value,
        long? limit = null,
        CancellationToken cancellationToken = default);
}

public enum JobStatus
{
    Submitted,
    Scheduled,
    Waiting,
    Running,
    Completed,
    Faulted,
    Cancelled
}

public enum JobAttemptStatus
{
    Running,
    Completed,
    Faulted,
    Cancelled
}

public enum JobControlOutcome
{
    Applied,
    Unchanged,
    InvalidState,
    NotFound,
    Unsupported
}

public sealed record JobSubmissionOptions
{
    public JobSubmissionOptions(Guid? jobId = null, Guid? recurringJobOccurrenceId = null)
    {
        if (jobId == Guid.Empty)
            throw new ArgumentException("The job identifier cannot be empty.", nameof(jobId));
        if (recurringJobOccurrenceId == Guid.Empty)
            throw new ArgumentException(
                "The recurring job occurrence identifier cannot be empty.",
                nameof(recurringJobOccurrenceId));

        JobId = jobId;
        RecurringJobOccurrenceId = recurringJobOccurrenceId;
    }

    public Guid? JobId { get; }

    public Guid? RecurringJobOccurrenceId { get; }
}

public sealed record JobSubmissionReceipt(
    Guid JobId,
    JobStatus Status,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? ScheduledForUtc = null);

public sealed record JobControlResult(
    JobControlOutcome Outcome,
    JobStatus? CurrentStatus = null);

public sealed record JobProgress
{
    public JobProgress(long value, long? limit = null)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Progress cannot be negative.");
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "The progress limit must be greater than zero.");
        if (limit is not null && value > limit)
            throw new ArgumentOutOfRangeException(nameof(value), "Progress cannot exceed its limit.");

        Value = value;
        Limit = limit;
    }

    public long Value { get; }

    public long? Limit { get; }
}

/// <summary>
/// Provider-neutral job state intended for inspection and monitoring. The job body is excluded.
/// </summary>
public sealed record JobState(
    Guid JobId,
    string JobType,
    JobStatus Status,
    string Provider,
    SchedulingDurability Durability,
    SchedulingPlacement Placement,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? ScheduledForUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    JobProgress? Progress,
    Guid? RecurringJobOccurrenceId,
    DateTimeOffset UpdatedAtUtc);

public sealed record JobAttemptState(
    Guid AttemptId,
    Guid JobId,
    int RetryAttempt,
    JobAttemptStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? FaultType,
    string? FaultMessage);

public interface IJobSource
{
    string Provider { get; }

    bool Authoritative { get; }

    Task<IReadOnlyList<JobState>> GetSnapshotAsync(
        int maximumCount,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobAttemptState>> GetAttemptsAsync(
        Guid jobId,
        int maximumCount,
        CancellationToken cancellationToken = default);
}

public interface IJobClient
{
    Task<JobSubmissionReceipt> Submit<TJob>(
        TJob job,
        JobSubmissionOptions? options = null,
        CancellationToken cancellationToken = default)
        where TJob : class;

    Task<JobSubmissionReceipt> Schedule<TJob>(
        DateTimeOffset startAtUtc,
        TJob job,
        JobSubmissionOptions? options = null,
        CancellationToken cancellationToken = default)
        where TJob : class;

    Task<JobControlResult> Cancel(
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task<JobControlResult> Retry(
        Guid jobId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provider integration boundary for tracked job storage and execution.
/// Applications use <see cref="IJobClient"/> instead.
/// </summary>
public interface IJobProvider : IJobClient, IJobSource
{
    string ProviderName { get; }

    SchedulingDurability Durability { get; }

    SchedulingPlacement Placement { get; }
}
