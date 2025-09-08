using System;
using System.Collections.Generic;

namespace MyServiceBus;

public interface IQueueSendContext : ISendContext
{
    TimeSpan? TimeToLive { get; set; }
    bool Persistent { get; set; }
    IDictionary<string, object> BrokerProperties { get; }
}
