using Microsoft.Extensions.Logging.Abstractions;
using MyServiceBus.Persistence;

namespace MyServiceBus.Tests;

public class OutboxDeliveryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Polls_with_the_configured_service_owner_and_lease()
    {
        var store = new RecordingStore();
        var dispatcher = new OutboxDispatcher(
            store,
            new NoOpTransport(),
            new ExponentialOutboxRetryPolicy(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1)),
            new FixedTimeProvider(Now));
        var options = new OutboxDeliveryOptions
        {
            OwnerId = "orders-service-replica-a",
            BatchSize = 25,
            LeaseDuration = TimeSpan.FromSeconds(30),
            PollInterval = TimeSpan.FromMinutes(1)
        };
        var service = new OutboxDeliveryService(
            dispatcher,
            options,
            NullLogger<OutboxDeliveryService>.Instance,
            new FixedTimeProvider(Now));

        await service.StartAsync(CancellationToken.None);
        var request = await store.FirstRequest.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => service.Status.LastSuccessfulPollAtUtc is not null);
        var runningStatus = service.Status;
        await service.StopAsync(CancellationToken.None);

        Assert.Equal("orders-service-replica-a", request.OwnerId);
        Assert.Equal(25, request.MaximumCount);
        Assert.Equal(Now, request.NowUtc);
        Assert.Equal(TimeSpan.FromSeconds(30), request.LeaseDuration);
        Assert.True(runningStatus.IsRunning);
        Assert.Equal(Now, runningStatus.LastPollAtUtc);
        Assert.Equal(Now, runningStatus.LastSuccessfulPollAtUtc);
        Assert.Equal(new OutboxDispatchBatchResult(0, 0, 0, 0), runningStatus.LastBatch);
        Assert.False(service.Status.IsRunning);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
            await Task.Delay(10, cancellation.Token);
    }

    private sealed class RecordingStore : IOutboxStore
    {
        public TaskCompletionSource<OutboxLeaseRequest> FirstRequest { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IReadOnlyList<OutboxLease>> LeaseAsync(
            OutboxLeaseRequest request,
            CancellationToken cancellationToken = default)
        {
            FirstRequest.TrySetResult(request);
            return Task.FromResult<IReadOnlyList<OutboxLease>>([]);
        }

        public Task<bool> MarkDispatchedAsync(
            Guid recordId,
            string ownerId,
            DateTimeOffset dispatchedAtUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> RescheduleAsync(
            Guid recordId,
            string ownerId,
            DateTimeOffset nextAttemptAtUtc,
            string failureCategory,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpTransport : IOutboxTransportDispatcher
    {
        public Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
