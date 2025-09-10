using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IMessageScheduler
{
    Task SchedulePublish<T>(T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class;
    Task SchedulePublish<T>(T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class;
    Task ScheduleSend<T>(Uri destination, T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class;
    Task ScheduleSend<T>(Uri destination, T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class;
}
