package com.myservicebus.topology;

import com.myservicebus.choreography.ChoreographyFragment;
import java.util.List;

public interface BusTopology {
    List<MessageTopology> getMessages();
    List<ConsumerTopology> getConsumers();

    default List<ConsumerDefinitionModel> getConsumerDefinitions() {
        return List.of();
    }
    List<ReceiveEndpointDefinition> getReceiveEndpoints();
    List<ChoreographyFragment> getChoreographies();
    default List<SagaStateMachineTopology> getSagaStateMachines() {
        return List.of();
    }

    default TopologySnapshot getSnapshot() {
        return TopologySnapshots.create(this);
    }
}
