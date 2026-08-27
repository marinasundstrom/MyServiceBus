package com.myservicebus.azure.servicebus;

import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;

import java.util.List;

record AzureServiceBusReceiveEndpointTopology(
        String queueName,
        boolean durable,
        boolean temporary,
        int prefetchCount,
        List<MessageBinding> bindings) {

    AzureServiceBusReceiveEndpointTopology {
        if (queueName == null || queueName.isBlank()) {
            throw new IllegalArgumentException("Azure Service Bus queue name cannot be blank");
        }
        if (durable && temporary) {
            throw new IllegalArgumentException("An Azure Service Bus endpoint cannot be both durable and temporary");
        }
        if (prefetchCount < 0) {
            throw new IllegalArgumentException("Azure Service Bus prefetch count cannot be negative");
        }
        if (bindings == null || bindings.isEmpty()) {
            throw new IllegalArgumentException("Azure Service Bus endpoint must have at least one binding");
        }
        if (bindings.stream().anyMatch(binding -> binding.getEntityName() == null
                || binding.getEntityName().isBlank())) {
            throw new IllegalArgumentException("Azure Service Bus topic binding name cannot be blank");
        }
        bindings = List.copyOf(bindings);
    }

    static AzureServiceBusReceiveEndpointTopology project(ReceiveEndpointTransportTopology topology) {
        if (topology.transportOptions() != null && !topology.transportOptions().isEmpty()) {
            throw new UnsupportedOperationException(
                    "Azure Service Bus transport options are not supported in the first preview slice");
        }
        return new AzureServiceBusReceiveEndpointTopology(
                topology.name(),
                topology.durable(),
                topology.temporary(),
                topology.prefetchCount(),
                topology.bindings());
    }
}
