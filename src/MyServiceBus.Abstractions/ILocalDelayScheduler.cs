using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

/// <summary>
/// Schedules callbacks within the current process. Pending callbacks are not durable and are lost
/// when the process stops. Durable message scheduling uses <see cref="IScheduleMessageProvider"/>.
/// </summary>
public interface ILocalDelayScheduler
{
    Task<Guid> Schedule(DateTime scheduledTime, Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default);
    Task<Guid> Schedule(TimeSpan delay, Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default) =>
        Schedule(DateTime.UtcNow + delay, callback, cancellationToken);
    Task<bool> Cancel(Guid tokenId);
}
