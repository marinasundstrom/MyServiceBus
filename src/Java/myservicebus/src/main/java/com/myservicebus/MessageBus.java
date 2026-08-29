package com.myservicebus;

import java.net.URI;
import java.time.Duration;

import com.myservicebus.topology.BusTopology;

public interface MessageBus extends PublishEndpoint, PublishEndpointProvider, SendEndpointProvider {
    BusFactory factory = new DefaultBusFactory();
    URI getAddress();

    BusTopology getTopology();

    /**
     * Starts the bus after validating explicitly required transport capabilities.
     *
     * @throws UnsupportedTransportCapabilityException when the selected transport cannot satisfy a requirement
     */
    void start() throws Exception;

    void stop() throws Exception;

    /**
     * Stops receiving and allows active work to drain within the supplied timeout.
     *
     * @throws BusStopTimeoutException when the timeout elapses before the drain completes
     */
    default void stop(Duration timeout) throws Exception {
        stop();
    }
}
