using System;
using System.Collections.Generic;

namespace MyServiceBus.Topology;

public class ConsumerTopology
{
    public Type ConsumerType { get; set; }
    public string Address { get; set; } = default!;
    public List<MessageBinding> Bindings { get; set; } = new();
    public Delegate? ConfigurePipe { get; set; }
    public ushort? ConcurrencyLimit { get; set; }
    public object? TransportSettings { get; set; }
    public Type? SerializerType { get; set; }
}
