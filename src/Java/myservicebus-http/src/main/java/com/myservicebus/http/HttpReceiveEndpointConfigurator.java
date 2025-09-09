package com.myservicebus.http;

import java.net.URI;

import com.myservicebus.EntityNameFormatter;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.TopologyRegistry;

public class HttpReceiveEndpointConfigurator {
    private final TopologyRegistry topology;
    private final URI baseAddress;
    private final String path;

    public HttpReceiveEndpointConfigurator(TopologyRegistry topology, URI baseAddress, String path) {
        this.topology = topology;
        this.baseAddress = baseAddress;
        this.path = path.startsWith("/") ? path.substring(1) : path;
    }

    public void configureConsumer(BusRegistrationContext context, Class<?> consumerClass) {
        ConsumerTopology def = topology.getConsumers().stream()
                .filter(c -> c.getConsumerType().equals(consumerClass))
                .findFirst()
                .orElseThrow(() -> new IllegalStateException(
                        "Consumer " + consumerClass.getSimpleName() + " not registered"));

        URI uri = baseAddress.resolve(this.path);
        def.setAddress(uri.toString());
        for (MessageBinding binding : def.getBindings()) {
            binding.setEntityName(EntityNameFormatter.format(binding.getMessageType()));
        }
    }
}

