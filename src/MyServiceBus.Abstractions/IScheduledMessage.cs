using System;

namespace MyServiceBus;

public interface IScheduledMessage
{
    DateTime? ScheduledEnqueueTime { get; set; }

    void SetScheduledEnqueueTime(DateTime scheduledTime) => ScheduledEnqueueTime = scheduledTime;

    void SetScheduledEnqueueTime(TimeSpan delay) => SetScheduledEnqueueTime(DateTime.UtcNow + delay);
}
