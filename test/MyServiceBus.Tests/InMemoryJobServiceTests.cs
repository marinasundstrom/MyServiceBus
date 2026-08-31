using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace MyServiceBus.Tests;

public class InMemoryJobServiceTests
{
    [Fact]
    public async Task Executes_a_registered_job_and_reports_progress()
    {
        await using var provider = CreateProvider<ProgressJobConsumer, ProgressJob>();
        var client = provider.GetRequiredService<IJobClient>();
        var source = provider.GetRequiredService<IJobSource>();

        var receipt = await client.Submit(new ProgressJob(7));
        var state = await WaitForState(source, receipt.JobId, JobStatus.Completed);

        state.JobType.ShouldBe(nameof(ProgressJob));
        state.Progress.ShouldBe(new JobProgress(7, 10));
        var attempts = await source.GetAttemptsAsync(receipt.JobId, 10);
        attempts.Count.ShouldBe(1);
        attempts[0].Status.ShouldBe(JobAttemptStatus.Completed);
    }

    [Fact]
    public async Task Retries_a_faulted_attempt()
    {
        var recorder = new JobRecorder();
        await using var provider = CreateProvider<RetryJobConsumer, RetryJob>(
            recorder,
            options => options.SetRetry(retry => retry.Immediate(2)));
        var client = provider.GetRequiredService<IJobClient>();
        var source = provider.GetRequiredService<IJobSource>();

        var receipt = await client.Submit(new RetryJob());
        await WaitForState(source, receipt.JobId, JobStatus.Completed);

        recorder.Attempts.ShouldBe(3);
        var attempts = await source.GetAttemptsAsync(receipt.JobId, 10);
        attempts.Select(attempt => attempt.Status).ShouldBe([
            JobAttemptStatus.Faulted,
            JobAttemptStatus.Faulted,
            JobAttemptStatus.Completed
        ]);
    }

    [Fact]
    public async Task Cancels_scheduled_work_before_it_starts()
    {
        var recorder = new JobRecorder();
        await using var provider = CreateProvider<CountingJobConsumer, CountingJob>(recorder);
        var client = provider.GetRequiredService<IJobClient>();
        var source = provider.GetRequiredService<IJobSource>();

        var receipt = await client.Schedule(DateTimeOffset.UtcNow.AddMinutes(1), new CountingJob());
        var result = await client.Cancel(receipt.JobId);

        result.Outcome.ShouldBe(JobControlOutcome.Applied);
        var state = await WaitForState(source, receipt.JobId, JobStatus.Cancelled);
        state.Status.ShouldBe(JobStatus.Cancelled);
        recorder.Attempts.ShouldBe(0);
    }

    [Fact]
    public async Task Enforces_the_per_consumer_concurrency_limit()
    {
        var recorder = new JobRecorder();
        await using var provider = CreateProvider<ConcurrentJobConsumer, ConcurrentJob>(
            recorder,
            options => options.SetConcurrentJobLimit(1));
        var client = provider.GetRequiredService<IJobClient>();
        var source = provider.GetRequiredService<IJobSource>();

        var first = await client.Submit(new ConcurrentJob());
        var second = await client.Submit(new ConcurrentJob());
        await WaitForState(source, first.JobId, JobStatus.Completed);
        await WaitForState(source, second.JobId, JobStatus.Completed);

        recorder.MaximumConcurrency.ShouldBe(1);
    }

    [Fact]
    public async Task Times_out_and_retries_long_running_work()
    {
        await using var provider = CreateProvider<TimeoutJobConsumer, TimeoutJob>(
            configure: options => options
                .SetJobTimeout(TimeSpan.FromMilliseconds(20))
                .SetRetry(retry => retry.Immediate(1)));
        var client = provider.GetRequiredService<IJobClient>();
        var source = provider.GetRequiredService<IJobSource>();

        var receipt = await client.Submit(new TimeoutJob());
        await WaitForState(source, receipt.JobId, JobStatus.Faulted);

        var attempts = await source.GetAttemptsAsync(receipt.JobId, 10);
        attempts.Count.ShouldBe(2);
        attempts.ShouldAllBe(attempt => attempt.FaultType == typeof(TimeoutException).FullName);
    }

    [Fact]
    public async Task Manually_retries_a_terminal_job()
    {
        var recorder = new JobRecorder();
        await using var provider = CreateProvider<ManualRetryJobConsumer, ManualRetryJob>(recorder);
        var client = provider.GetRequiredService<IJobClient>();
        var source = provider.GetRequiredService<IJobSource>();

        var receipt = await client.Submit(new ManualRetryJob());
        await WaitForState(source, receipt.JobId, JobStatus.Faulted);
        (await client.Retry(receipt.JobId)).Outcome.ShouldBe(JobControlOutcome.Applied);
        await WaitForState(source, receipt.JobId, JobStatus.Completed);

        recorder.Attempts.ShouldBe(2);
    }

    private static ServiceProvider CreateProvider<TConsumer, TJob>(
        JobRecorder? recorder = null,
        Action<JobConsumerOptions>? configure = null)
        where TConsumer : class, IJobConsumer<TJob>
        where TJob : class
    {
        var services = new ServiceCollection();
        services.AddSingleton(recorder ?? new JobRecorder());
        services.AddServiceBus(configurator =>
        {
            configurator.AddJobConsumer<TConsumer, TJob>(configure);
            configurator.UsingMediator();
        });
        return services.BuildServiceProvider();
    }

    private static async Task<JobState> WaitForState(
        IJobSource source,
        Guid jobId,
        JobStatus expected)
    {
        var timeout = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < timeout)
        {
            var state = (await source.GetSnapshotAsync(100)).Single(job => job.JobId == jobId);
            if (state.Status == expected)
                return state;
            await Task.Delay(10);
        }

        var last = (await source.GetSnapshotAsync(100)).Single(job => job.JobId == jobId);
        throw new TimeoutException($"Job '{jobId}' remained in state {last.Status}.");
    }

    private sealed record ProgressJob(int Value);

    private sealed class ProgressJobConsumer : IJobConsumer<ProgressJob>
    {
        public async Task Run(JobContext<ProgressJob> context)
        {
            await context.SetProgress(context.Job.Value, 10);
        }
    }

    private sealed record RetryJob;

    private sealed class RetryJobConsumer(JobRecorder recorder) : IJobConsumer<RetryJob>
    {
        public Task Run(JobContext<RetryJob> context)
        {
            if (recorder.IncrementAttempts() < 3)
                throw new InvalidOperationException("Try again");
            return Task.CompletedTask;
        }
    }

    private sealed record CountingJob;

    private sealed class CountingJobConsumer(JobRecorder recorder) : IJobConsumer<CountingJob>
    {
        public Task Run(JobContext<CountingJob> context)
        {
            recorder.IncrementAttempts();
            return Task.CompletedTask;
        }
    }

    private sealed record ConcurrentJob;

    private sealed class ConcurrentJobConsumer(JobRecorder recorder) : IJobConsumer<ConcurrentJob>
    {
        public async Task Run(JobContext<ConcurrentJob> context)
        {
            recorder.Enter();
            try
            {
                await Task.Delay(30, context.CancellationToken);
            }
            finally
            {
                recorder.Exit();
            }
        }
    }

    private sealed record TimeoutJob;

    private sealed class TimeoutJobConsumer : IJobConsumer<TimeoutJob>
    {
        public Task Run(JobContext<TimeoutJob> context) =>
            Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
    }

    private sealed record ManualRetryJob;

    private sealed class ManualRetryJobConsumer(JobRecorder recorder) : IJobConsumer<ManualRetryJob>
    {
        public Task Run(JobContext<ManualRetryJob> context)
        {
            if (recorder.IncrementAttempts() == 1)
                throw new InvalidOperationException("Retry manually");
            return Task.CompletedTask;
        }
    }

    private sealed class JobRecorder
    {
        private int attempts;
        private int concurrency;
        private int maximumConcurrency;

        public int Attempts => attempts;

        public int MaximumConcurrency => maximumConcurrency;

        public int IncrementAttempts() => Interlocked.Increment(ref attempts);

        public void Enter()
        {
            var current = Interlocked.Increment(ref concurrency);
            while (true)
            {
                var maximum = maximumConcurrency;
                if (current <= maximum || Interlocked.CompareExchange(ref maximumConcurrency, current, maximum) == maximum)
                    return;
            }
        }

        public void Exit() => Interlocked.Decrement(ref concurrency);
    }
}
