package com.myservicebus.http;

import java.net.URI;
import java.util.function.Consumer;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.TopologyRegistry;

public class HttpFactoryConfigurator {
    private final TopologyRegistry topology;
    private URI baseAddress = URI.create("http://localhost/");

    public HttpFactoryConfigurator(BusRegistrationConfigurator cfg) {
        this.topology = cfg.getTopologyRegistry();
    }

    public void host(URI address) {
        this.baseAddress = address;
        for (ConsumerTopology consumer : topology.getConsumers()) {
            String addr = consumer.getAddress();
            if (addr != null && !addr.contains("://")) {
                URI uri = baseAddress.resolve(addr);
                consumer.setAddress(uri.toString());
            }
        }
    }

    public URI getBaseAddress() {
        return baseAddress;
    }

    public void receiveEndpoint(String path, Consumer<HttpReceiveEndpointConfigurator> configure) {
        if (configure != null) {
            HttpReceiveEndpointConfigurator endpointConfigurator = new HttpReceiveEndpointConfigurator(topology, baseAddress,
                    path);
            configure.accept(endpointConfigurator);
        }
    }
}
