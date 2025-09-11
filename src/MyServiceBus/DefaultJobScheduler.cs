using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public class DefaultJobScheduler : IJobScheduler
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _jobs = new();

    public Task<Guid> Schedule(DateTime scheduledTime, Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _jobs[id] = cts;
        _ = Execute(id, scheduledTime, callback, cts);
        return Task.FromResult(id);
    }

    public Task<Guid> Schedule(TimeSpan delay, Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default)
        => Schedule(DateTime.UtcNow + delay, callback, cancellationToken);

    [Throws(typeof(AggregateException))]
    public Task Cancel(Guid tokenId)
    {
        if (_jobs.TryRemove(tokenId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
        return Task.CompletedTask;
    }

    private async Task Execute(Guid id, DateTime scheduledTime, Func<CancellationToken, Task> callback, CancellationTokenSource cts)
    {
        try
        {
            var delay = scheduledTime - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cts.Token).ConfigureAwait(false);

            if (!cts.IsCancellationRequested)
                await callback(cts.Token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            _jobs.TryRemove(id, out _);
            cts.Dispose();
        }
    }
}
