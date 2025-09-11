using System;

namespace MyServiceBus;

public record ScheduledMessageHandle(Guid TokenId, DateTime ScheduledTime);
