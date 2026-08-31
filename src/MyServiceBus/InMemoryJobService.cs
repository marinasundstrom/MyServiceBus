using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public sealed class JobExecutionContext
{
    private readonly Action<JobProgress> setProgress;
    private readonly DateTimeOffset startedAtUtc;

    public JobExecutionContext(
        Guid jobId,
        Guid attemptId,
        int retryAttempt,
        object job,
        CancellationToken cancellationToken,
        DateTimeOffset startedAtUtc,
        Action<JobProgress> setProgress)
    {
        JobId = jobId;
        AttemptId = attemptId;
        RetryAttempt = retryAttempt;
        Job = job;
        CancellationToken = cancellationToken;
        this.startedAtUtc = startedAtUtc;
        this.setProgress = setProgress;
    }

    public Guid JobId { get; }

    public Guid AttemptId { get; }

    public int RetryAttempt { get; }

    public object Job { get; }

    public CancellationToken CancellationToken { get; }

    public TimeSpan ElapsedTime => DateTimeOffset.UtcNow - startedAtUtc;

    public Task SetProgress(long value, long? limit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancellationToken.ThrowIfCancellationRequested();
        setProgress(new JobProgress(value, limit));
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryJobContext<TJob> : JobContext<TJob>
    where TJob : class
{
    private readonly JobExecutionContext context;

    public InMemoryJobContext(JobExecutionContext context, TJob job)
    {
        this.context = context;
        Job = job;
    }

    public Guid JobId => context.JobId;

    public Guid AttemptId => context.AttemptId;

    public int RetryAttempt => context.RetryAttempt;

    public TJob Job { get; }

    public TimeSpan ElapsedTime => context.ElapsedTime;

    public CancellationToken CancellationToken => context.CancellationToken;

    public Task SetProgress(long value, long? limit = null, CancellationToken cancellationToken = default) =>
        context.SetProgress(value, limit, cancellationToken);
}

internal sealed class InMemoryJobService : IJobProvider
{
    private sealed class Entry
    {
        public required Guid JobId { get; init; }
        public required object Job { get; init; }
        public required IRegisteredJobConsumer Descriptor { get; init; }
        public required DateTimeOffset SubmittedAtUtc { get; init; }
        public DateTimeOffset? ScheduledForUtc { get; init; }
        public JobStatus Status { get; set; }
        public DateTimeOffset? StartedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public JobProgress? Progress { get; set; }
        public CancellationTokenSource Cancellation { get; set; } = new();
        public Guid? ScheduleToken { get; set; }
        public List<JobAttemptState> Attempts { get; } = [];
        public object Sync { get; } = new();
    }

    private readonly ConcurrentDictionary<Guid, Entry> jobs = new();
    private readonly JobConsumerRegistry registry;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILocalDelayScheduler delayScheduler;

    public InMemoryJobService(
        JobConsumerRegistry registry,
        IServiceScopeFactory scopeFactory,
        ILocalDelayScheduler delayScheduler)
    {
        this.registry = registry;
        this.scopeFactory = scopeFactory;
        this.delayScheduler = delayScheduler;
    }

    public string ProviderName => "in-memory";

    public SchedulingDurability Durability => SchedulingDurability.Volatile;

    public SchedulingPlacement Placement => SchedulingPlacement.ProcessLocal;

    string IJobSource.Provider => ProviderName;

    public bool Authoritative => true;

    public Task<JobSubmissionReceipt> Submit<TJob>(
        TJob job,
        JobSubmissionOptions? options = null,
        CancellationToken cancellationToken = default)
        where TJob : class
    {
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        var entry = CreateEntry(job, options, now, null, JobStatus.Waiting);
        _ = Execute(entry);
        return Task.FromResult(CreateReceipt(entry));
    }

    public async Task<JobSubmissionReceipt> Schedule<TJob>(
        DateTimeOffset startAtUtc,
        TJob job,
        JobSubmissionOptions? options = null,
        CancellationToken cancellationToken = default)
        where TJob : class
    {
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        var scheduled = startAtUtc.ToUniversalTime();
        var entry = CreateEntry(job, options, now, scheduled, JobStatus.Scheduled);
        entry.ScheduleToken = await delayScheduler.Schedule(
            scheduled.UtcDateTime,
            _ =>
            {
                lock (entry.Sync)
                {
                    if (entry.Status == JobStatus.Cancelled)
                        return Task.CompletedTask;
                    entry.Status = JobStatus.Waiting;
                    entry.UpdatedAtUtc = DateTimeOffset.UtcNow;
                }
                return Execute(entry);
            },
            CancellationToken.None).ConfigureAwait(false);
        return CreateReceipt(entry);
    }

    public async Task<JobControlResult> Cancel(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!jobs.TryGetValue(jobId, out var entry))
            return new JobControlResult(JobControlOutcome.NotFound);

        Guid? scheduleToken;
        lock (entry.Sync)
        {
            if (entry.Status is JobStatus.Completed or JobStatus.Faulted or JobStatus.Cancelled)
                return new JobControlResult(JobControlOutcome.Unchanged, entry.Status);
            entry.Cancellation.Cancel();
            scheduleToken = entry.ScheduleToken;
            if (entry.Status is JobStatus.Scheduled or JobStatus.Waiting)
                Complete(entry, JobStatus.Cancelled);
        }

        if (scheduleToken is not null)
            await delayScheduler.Cancel(scheduleToken.Value).ConfigureAwait(false);
        return new JobControlResult(JobControlOutcome.Applied, entry.Status);
    }

    public Task<JobControlResult> Retry(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!jobs.TryGetValue(jobId, out var entry))
            return Task.FromResult(new JobControlResult(JobControlOutcome.NotFound));

        lock (entry.Sync)
        {
            if (entry.Status is not (JobStatus.Faulted or JobStatus.Cancelled))
                return Task.FromResult(new JobControlResult(JobControlOutcome.InvalidState, entry.Status));
            entry.Cancellation.Dispose();
            entry.Cancellation = new CancellationTokenSource();
            entry.Status = JobStatus.Waiting;
            entry.CompletedAtUtc = null;
            entry.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        _ = Execute(entry);
        return Task.FromResult(new JobControlResult(JobControlOutcome.Applied, JobStatus.Waiting));
    }

    public Task<IReadOnlyList<JobState>> GetSnapshotAsync(
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        if (maximumCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<JobState> snapshot = jobs.Values
            .OrderByDescending(entry => entry.UpdatedAtUtc)
            .Take(maximumCount)
            .Select(CreateState)
            .ToArray();
        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<JobAttemptState>> GetAttemptsAsync(
        Guid jobId,
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        if (maximumCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        cancellationToken.ThrowIfCancellationRequested();
        if (!jobs.TryGetValue(jobId, out var entry))
            return Task.FromResult<IReadOnlyList<JobAttemptState>>([]);
        lock (entry.Sync)
            return Task.FromResult<IReadOnlyList<JobAttemptState>>(entry.Attempts.TakeLast(maximumCount).ToArray());
    }

    private Entry CreateEntry<TJob>(
        TJob job,
        JobSubmissionOptions? options,
        DateTimeOffset submittedAtUtc,
        DateTimeOffset? scheduledForUtc,
        JobStatus status)
        where TJob : class
    {
        var jobId = options?.JobId ?? Guid.NewGuid();
        var entry = new Entry
        {
            JobId = jobId,
            Job = job,
            Descriptor = registry.Get(typeof(TJob)),
            SubmittedAtUtc = submittedAtUtc,
            ScheduledForUtc = scheduledForUtc,
            Status = status,
            UpdatedAtUtc = submittedAtUtc
        };
        if (!jobs.TryAdd(jobId, entry))
            throw new InvalidOperationException($"Job '{jobId}' already exists.");
        return entry;
    }

    private async Task Execute(Entry entry)
    {
        var descriptor = entry.Descriptor;
        for (var retryAttempt = 0; ; retryAttempt++)
        {
            try
            {
                await descriptor.Concurrency.WaitAsync(entry.Cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                lock (entry.Sync)
                    Complete(entry, JobStatus.Cancelled);
                return;
            }

            try
            {
                if (entry.Cancellation.IsCancellationRequested)
                {
                    lock (entry.Sync)
                        Complete(entry, JobStatus.Cancelled);
                    return;
                }

                var startedAtUtc = DateTimeOffset.UtcNow;
                var attemptId = Guid.NewGuid();
                lock (entry.Sync)
                {
                    entry.Status = JobStatus.Running;
                    entry.StartedAtUtc ??= startedAtUtc;
                    entry.UpdatedAtUtc = startedAtUtc;
                    entry.Attempts.Add(new JobAttemptState(
                        attemptId,
                        entry.JobId,
                        retryAttempt,
                        JobAttemptStatus.Running,
                        startedAtUtc,
                        null,
                        null,
                        null));
                }

                using var timeout = new CancellationTokenSource(descriptor.Options.JobTimeout);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    entry.Cancellation.Token,
                    timeout.Token);
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var context = new JobExecutionContext(
                        entry.JobId,
                        attemptId,
                        retryAttempt,
                        entry.Job,
                        linked.Token,
                        startedAtUtc,
                        progress =>
                        {
                            lock (entry.Sync)
                            {
                                entry.Progress = progress;
                                entry.UpdatedAtUtc = DateTimeOffset.UtcNow;
                            }
                        });
                    await descriptor.Run(scope.ServiceProvider, context).WaitAsync(linked.Token).ConfigureAwait(false);
                    lock (entry.Sync)
                    {
                        FinishAttempt(entry, attemptId, JobAttemptStatus.Completed, null);
                        Complete(entry, JobStatus.Completed);
                    }
                    return;
                }
                catch (OperationCanceledException) when (entry.Cancellation.IsCancellationRequested)
                {
                    lock (entry.Sync)
                    {
                        FinishAttempt(entry, attemptId, JobAttemptStatus.Cancelled, null);
                        Complete(entry, JobStatus.Cancelled);
                    }
                    return;
                }
                catch (Exception exception)
                {
                    var failure = timeout.IsCancellationRequested
                        ? new TimeoutException($"Job '{entry.JobId}' exceeded its timeout of {descriptor.Options.JobTimeout}.", exception)
                        : exception;
                    lock (entry.Sync)
                        FinishAttempt(entry, attemptId, JobAttemptStatus.Faulted, failure);

                    if (retryAttempt >= descriptor.Options.RetryCount)
                    {
                        lock (entry.Sync)
                            Complete(entry, JobStatus.Faulted);
                        return;
                    }

                    lock (entry.Sync)
                    {
                        entry.Status = JobStatus.Waiting;
                        entry.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    }
                    if (descriptor.Options.RetryDelay is { } delay)
                        await Task.Delay(delay, entry.Cancellation.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                lock (entry.Sync)
                    Complete(entry, JobStatus.Cancelled);
                return;
            }
            finally
            {
                descriptor.Concurrency.Release();
            }
        }
    }

    private static void FinishAttempt(
        Entry entry,
        Guid attemptId,
        JobAttemptStatus status,
        Exception? exception)
    {
        var index = entry.Attempts.FindIndex(attempt => attempt.AttemptId == attemptId);
        var current = entry.Attempts[index];
        entry.Attempts[index] = current with
        {
            Status = status,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            FaultType = exception?.GetType().FullName,
            FaultMessage = exception?.Message
        };
    }

    private static void Complete(Entry entry, JobStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        entry.Status = status;
        entry.CompletedAtUtc = now;
        entry.UpdatedAtUtc = now;
    }

    private static JobSubmissionReceipt CreateReceipt(Entry entry) =>
        new(entry.JobId, entry.Status, entry.SubmittedAtUtc, entry.ScheduledForUtc);

    private JobState CreateState(Entry entry)
    {
        lock (entry.Sync)
        {
            return new JobState(
                entry.JobId,
                entry.Descriptor.JobTypeName,
                entry.Status,
                ProviderName,
                Durability,
                Placement,
                entry.SubmittedAtUtc,
                entry.ScheduledForUtc,
                entry.StartedAtUtc,
                entry.CompletedAtUtc,
                entry.Progress,
                null,
                entry.UpdatedAtUtc);
        }
    }
}
