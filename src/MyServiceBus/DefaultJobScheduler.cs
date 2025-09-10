using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public class DefaultJobScheduler : IJobScheduler
{
    public async Task Schedule(DateTime scheduledTime, Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default)
    {
        var delay = scheduledTime - DateTime.UtcNow;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

        await callback(cancellationToken).ConfigureAwait(false);
    }

    public Task Schedule(TimeSpan delay, Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default)
        => Schedule(DateTime.UtcNow + delay, callback, cancellationToken);
}
