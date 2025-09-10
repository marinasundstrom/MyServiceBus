using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IJobScheduler
{
    Task Schedule(DateTime scheduledTime, Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default);
    Task Schedule(TimeSpan delay, Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default) =>
        Schedule(DateTime.UtcNow + delay, callback, cancellationToken);
}
