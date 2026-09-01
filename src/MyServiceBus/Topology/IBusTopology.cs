using System.Collections.Generic;
using MyServiceBus.Choreography;

namespace MyServiceBus.Topology;

public interface IBusTopology
{
    List<MessageTopology> Messages { get; }
    List<ConsumerTopology> Consumers { get; }
    IReadOnlyList<ReceiveEndpointDefinition> ReceiveEndpoints { get; }
    IReadOnlyList<ChoreographyFragment> Choreographies { get; }
    IReadOnlyList<SagaStateMachineTopology> SagaStateMachines => [];

    TopologySnapshot GetSnapshot() => TopologySnapshotBuilder.Create(this);
}
