package com.myservicebus.topology;

import java.util.List;

public interface BusTopology {
    List<MessageTopology> getMessages();
    List<ConsumerTopology> getConsumers();
    List<ReceiveEndpointDefinition> getReceiveEndpoints();

    default TopologySnapshot getSnapshot() {
        return TopologySnapshots.create(this);
    }
}
