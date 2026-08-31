using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace MyServiceBus.Tests;

public class InMemoryRecurringJobProviderTests
{
    private sealed record TestJob(string Value);

    private sealed class JobRecorder
    {
        public TaskCompletionSource<string> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class TestJobConsumer(JobRecorder recorder) : IJobConsumer<TestJob>
    {
        public Task Run(JobContext<TestJob> context)
        {
            recorder.Completion.TrySetResult(context.Job.Value);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingJobClient : IJobClient
    {
        public List<object> Messages { get; } = [];

        public Task<JobSubmissionReceipt> Submit<TJob>(
            TJob job,
            JobSubmissionOptions? options = null,
            CancellationToken cancellationToken = default)
            where TJob : class
        {
            Messages.Add(job);
            return Task.FromResult(new JobSubmissionReceipt(
                options?.JobId ?? Guid.NewGuid(),
                JobStatus.Waiting,
                DateTimeOffset.UtcNow));
        }

        public Task<JobSubmissionReceipt> Schedule<TJob>(
            DateTimeOffset startAtUtc,
            TJob job,
            JobSubmissionOptions? options = null,
            CancellationToken cancellationToken = default)
            where TJob : class => throw new NotSupportedException();

        public Task<JobControlResult> Cancel(Guid jobId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobControlResult> Retry(Guid jobId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ManualDelayScheduler : ILocalDelayScheduler
    {
        private readonly Dictionary<Guid, ScheduledCallback> callbacks = [];

        private sealed record ScheduledCallback(
            DateTime ScheduledTime,
            Func<CancellationToken, Task> Callback);

        public int Count => callbacks.Count;

        public DateTime? NextScheduledTime => callbacks.Values.FirstOrDefault()?.ScheduledTime;

        public Task<Guid> Schedule(
            DateTime scheduledTime,
            Func<CancellationToken, Task> callback,
            CancellationToken cancellationToken = default)
        {
            var token = Guid.NewGuid();
            callbacks[token] = new(scheduledTime, callback);
            return Task.FromResult(token);
        }

        public Task<bool> Cancel(Guid tokenId) => Task.FromResult(callbacks.Remove(tokenId));

        public async Task RunNext()
        {
            var next = callbacks.First();
            callbacks.Remove(next.Key);
            await next.Value.Callback(CancellationToken.None);
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void SetUtcNow(DateTimeOffset value) => now = value;
    }

    private sealed record FixedIntervalFixture(
        string Name,
        string Policy,
        int MaxCatchUpOccurrences,
        DateTimeOffset NowUtc,
        int ExpectedDispatchCount,
        DateTimeOffset ExpectedNextUtc);

    private sealed class CustomProvider : IRecurringJobProvider
    {
        public string ProviderName => "custom";

        public SchedulingDurability Durability => SchedulingDurability.Durable;

        public SchedulingPlacement Placement => SchedulingPlacement.RemoteService;

        public Task<RecurringJobDefinitionReceipt> AddOrUpdate<TJob>(
            RecurringJobDefinition definition,
            TJob job,
            long? expectedRevision = null,
            CancellationToken cancellationToken = default)
            where TJob : class => throw new NotImplementedException();

        public Task<RecurringJobControlResult> Pause(
            RecurringJobIdentity identity,
            long? expectedRevision = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<RecurringJobControlResult> Resume(
            RecurringJobIdentity identity,
            long? expectedRevision = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<RecurringJobControlResult> Remove(
            RecurringJobIdentity identity,
            long? expectedRevision = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<RecurringJobOccurrenceReceipt> TriggerNow(
            RecurringJobIdentity identity,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task Add_or_update_is_idempotent_and_revisions_changed_content()
    {
        var publisher = new RecordingJobClient();
        var delays = new ManualDelayScheduler();
        var provider = CreateProvider(publisher, delays);
        var identity = new RecurringJobIdentity("daily-export", "billing");
        var definition = new RecurringJobDefinition(
            identity,
            new FixedIntervalRecurringJobCadence(TimeSpan.FromHours(1)));
        var job = new TestJob("first");

        var first = await provider.AddOrUpdate(definition, job);
        var repeated = await provider.AddOrUpdate(definition, job);

        Assert.Equal(first.DefinitionId, repeated.DefinitionId);
        Assert.Equal(1, repeated.Revision);
        Assert.Equal(SchedulingDurability.Volatile, repeated.Durability);
        Assert.Equal(SchedulingPlacement.ProcessLocal, repeated.Placement);
        Assert.Equal(DateTimeOffset.Parse("2026-09-01T01:00:00Z"), repeated.NextOccurrenceAtUtc);
        Assert.Equal(1, delays.Count);

        var changed = await provider.AddOrUpdate(
            definition,
            new TestJob("changed"),
            expectedRevision: 1);

        Assert.Equal(first.DefinitionId, changed.DefinitionId);
        Assert.Equal(2, changed.Revision);
        Assert.Equal(1, delays.Count);
    }

    [Fact]
    public async Task Revision_conflicts_and_controls_are_explicit()
    {
        var delays = new ManualDelayScheduler();
        var provider = CreateProvider(new RecordingJobClient(), delays);
        var identity = new RecurringJobIdentity("daily-export");
        var definition = new RecurringJobDefinition(
            identity,
            new FixedIntervalRecurringJobCadence(TimeSpan.FromHours(1)));
        await provider.AddOrUpdate(definition, new TestJob("first"));

        var conflict = await Assert.ThrowsAsync<RecurringJobRevisionConflictException>(() =>
            provider.Pause(identity, expectedRevision: 99));
        Assert.Equal(1, conflict.CurrentRevision);

        var paused = await provider.Pause(identity, expectedRevision: 1);
        Assert.Equal(RecurringJobControlOutcome.Applied, paused.Outcome);
        Assert.Equal(2, paused.CurrentRevision);
        Assert.Equal(0, delays.Count);

        var resumed = await provider.Resume(identity, expectedRevision: 2);
        Assert.Equal(3, resumed.CurrentRevision);
        Assert.Equal(1, delays.Count);

        var removed = await provider.Remove(identity, expectedRevision: 3);
        Assert.Equal(4, removed.CurrentRevision);
        Assert.Equal(0, delays.Count);
        Assert.Equal(RecurringJobControlOutcome.NotFound, (await provider.Remove(identity)).Outcome);
    }

    [Fact]
    public async Task Due_and_manual_occurrences_submit_tracked_jobs()
    {
        var publisher = new RecordingJobClient();
        var delays = new ManualDelayScheduler();
        var provider = CreateProvider(publisher, delays);
        var identity = new RecurringJobIdentity("daily-export");
        await provider.AddOrUpdate(
            new RecurringJobDefinition(
                identity,
                new FixedIntervalRecurringJobCadence(TimeSpan.FromHours(1))),
            new TestJob("run"));

        await delays.RunNext();
        Assert.Single(publisher.Messages);
        Assert.Equal(1, delays.Count);

        var manual = await provider.TriggerNow(identity);
        Assert.True(manual.IsManual);
        Assert.Equal(RecurringJobOccurrenceStatus.Pending, manual.Status);
        Assert.Equal(2, publisher.Messages.Count);
        Assert.Equal(1, delays.Count);
    }

    [Fact]
    public async Task Default_registration_executes_manual_occurrence_as_tracked_job()
    {
        var recorder = new JobRecorder();
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddServiceBus(configurator =>
        {
            configurator.AddJobConsumer<TestJobConsumer, TestJob>();
            configurator.UsingMediator();
        });
        using var serviceProvider = services.BuildServiceProvider();
        var recurring = serviceProvider.GetRequiredService<IRecurringJobProvider>();
        var identity = new RecurringJobIdentity("tracked-manual");
        await recurring.AddOrUpdate(
            new RecurringJobDefinition(
                identity,
                new FixedIntervalRecurringJobCadence(TimeSpan.FromHours(1))),
            new TestJob("executed"));

        var occurrence = await recurring.TriggerNow(identity);
        Assert.Equal("executed", await recorder.Completion.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        var job = Assert.Single(await serviceProvider.GetRequiredService<IJobSource>().GetSnapshotAsync(10));

        Assert.Equal(occurrence.OccurrenceId, job.RecurringJobOccurrenceId);
        Assert.Equal(JobStatus.Completed, job.Status);
    }

    [Fact]
    public async Task Unsupported_cadence_and_overlap_are_rejected()
    {
        var provider = CreateProvider(new RecordingJobClient(), new ManualDelayScheduler());

        await Assert.ThrowsAsync<NotSupportedException>(() => provider.AddOrUpdate(
            new RecurringJobDefinition(
                new RecurringJobIdentity("cron"),
                new CronRecurringJobCadence("0 1 * * *", RecurringJobCronDialect.Unix5)),
            new TestJob("run")));
        await Assert.ThrowsAsync<NotSupportedException>(() => provider.AddOrUpdate(
            new RecurringJobDefinition(
                new RecurringJobIdentity("serial"),
                new FixedIntervalRecurringJobCadence(TimeSpan.FromHours(1)),
                overlapPolicy: RecurringJobOverlapPolicy.Forbid),
            new TestJob("run")));
    }

    [Fact]
    public void AddServiceBus_preserves_an_explicit_provider_registration()
    {
        var custom = new CustomProvider();
        var services = new ServiceCollection();
        services.AddSingleton<IRecurringJobProvider>(custom);
        services.AddServiceBus(configurator => configurator.UsingMediator());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Same(custom, serviceProvider.GetRequiredService<IRecurringJobProvider>());
        Assert.IsType<RecurringJobScheduler>(serviceProvider.GetRequiredService<IRecurringJobScheduler>());
    }

    [Fact]
    public async Task Fixed_interval_misfires_match_shared_cross_language_fixtures()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "scheduling-fixtures",
            "fixed-interval-misfires.json");
        var fixtures = JsonSerializer.Deserialize<List<FixedIntervalFixture>>(
            await File.ReadAllTextAsync(fixturePath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(fixtures);

        foreach (var fixture in fixtures)
        {
            var publisher = new RecordingJobClient();
            var delays = new ManualDelayScheduler();
            var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-09-01T00:00:00Z"));
            var provider = new InMemoryRecurringJobProvider(publisher, delays, clock);
            await provider.AddOrUpdate(
                new RecurringJobDefinition(
                    new RecurringJobIdentity(fixture.Name),
                    new FixedIntervalRecurringJobCadence(
                        TimeSpan.FromHours(1),
                        DateTimeOffset.Parse("2026-09-01T00:00:00Z")),
                    misfirePolicy: Enum.Parse<RecurringJobMisfirePolicy>(fixture.Policy),
                    maxCatchUpOccurrences: fixture.MaxCatchUpOccurrences),
                new TestJob(fixture.Name));

            clock.SetUtcNow(fixture.NowUtc);
            await delays.RunNext();

            Assert.Equal(fixture.ExpectedDispatchCount, publisher.Messages.Count);
            Assert.Equal(fixture.ExpectedNextUtc.UtcDateTime, delays.NextScheduledTime);
        }
    }

    private static InMemoryRecurringJobProvider CreateProvider(
        RecordingJobClient publisher,
        ManualDelayScheduler delays) =>
        new(
            publisher,
            delays,
            new ManualTimeProvider(DateTimeOffset.Parse("2026-09-01T00:00:00Z")));
}
