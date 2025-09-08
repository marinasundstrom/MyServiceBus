using System;
using System.Collections.Generic;

namespace MyServiceBus.Topology;

public class ConsumerTopology
{
    public Type ConsumerType { get; set; }
    public string QueueName { get; set; }
    public List<MessageBinding> Bindings { get; set; } = new();
    public Delegate? ConfigurePipe { get; set; }
    public ushort? PrefetchCount { get; set; }
    public IDictionary<string, object?>? QueueArguments { get; set; }
}
