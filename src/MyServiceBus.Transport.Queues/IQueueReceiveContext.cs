using System.Collections.Generic;

namespace MyServiceBus;

public interface IQueueReceiveContext : ReceiveContext
{
    long DeliveryCount { get; }
    string? Destination { get; }
    IDictionary<string, object> BrokerProperties { get; }
}
