package com.myservicebus;

import java.net.URI;

import com.myservicebus.topology.BusTopology;

public interface MessageBus extends PublishEndpoint, PublishEndpointProvider, SendEndpointProvider {
    URI getAddress();

    BusTopology getTopology();

    void start() throws Exception;

    void stop() throws Exception;
}
