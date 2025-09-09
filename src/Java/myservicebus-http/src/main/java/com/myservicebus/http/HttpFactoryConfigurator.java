package com.myservicebus.http;

import java.net.URI;
import java.util.function.Consumer;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.topology.TopologyRegistry;

public class HttpFactoryConfigurator {
    private final TopologyRegistry topology;
    private final URI baseAddress;

    public HttpFactoryConfigurator(BusRegistrationConfigurator cfg, URI baseAddress) {
        this.topology = cfg.getTopologyRegistry();
        this.baseAddress = baseAddress;
    }

    public void receiveEndpoint(String path, Consumer<HttpReceiveEndpointConfigurator> configure) {
        if (configure != null) {
            HttpReceiveEndpointConfigurator endpointConfigurator = new HttpReceiveEndpointConfigurator(topology, baseAddress,
                    path);
            configure.accept(endpointConfigurator);
        }
    }
}

