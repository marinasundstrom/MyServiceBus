using System.Collections.Generic;

namespace MyServiceBus.Topology;

public interface IBusTopology
{
    List<MessageTopology> Messages { get; }
    List<ConsumerTopology> Consumers { get; }
}

