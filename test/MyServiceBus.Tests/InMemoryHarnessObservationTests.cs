using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MyServiceBus.Tests;

public class InMemoryHarnessObservationTests
{
    record ObservedMessage;

    [Fact]
    public async Task WaitForConsumed_completes_after_successful_consumer_completion()
    {
        var harness = new InMemoryTestHarness();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.RegisterHandler<ObservedMessage>(_ => release.Task);
        await harness.Start();

        var observation = harness.WaitForConsumed<ObservedMessage>(TimeSpan.FromSeconds(1));
        var delivery = harness.Publish(new ObservedMessage());

        Assert.False(observation.IsCompleted);
        release.SetResult();

        await delivery;
        Assert.True(await observation);
        Assert.True(await harness.WaitForConsumed<ObservedMessage>(TimeSpan.Zero));
    }

    [Fact]
    public async Task WaitForConsumed_returns_false_when_timeout_elapses()
    {
        var harness = new InMemoryTestHarness();
        await harness.Start();

        Assert.False(await harness.WaitForConsumed<ObservedMessage>(TimeSpan.FromMilliseconds(10)));
    }

    [Fact]
    public async Task WaitForConsumed_propagates_caller_cancellation()
    {
        var harness = new InMemoryTestHarness();
        await harness.Start();
        using var source = new CancellationTokenSource();

        var observation = harness.WaitForConsumed<ObservedMessage>(Timeout.InfiniteTimeSpan, source.Token);
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => observation);
    }
}
