using System;

namespace MyServiceBus;

public interface IQueueSendContext : ISendContext
{
    string? RoutingKey { get; set; }
}

