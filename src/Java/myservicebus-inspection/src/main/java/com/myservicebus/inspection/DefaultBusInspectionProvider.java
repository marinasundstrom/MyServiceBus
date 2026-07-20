package com.myservicebus.inspection;

import com.myservicebus.MessageBus;
import com.myservicebus.topology.TopologySnapshot;

import java.time.Instant;
import java.util.Map;
import java.util.function.Function;
import java.util.stream.Collectors;

public final class DefaultBusInspectionProvider implements BusInspectionProvider {
    private final MessageBus bus;

    public DefaultBusInspectionProvider(MessageBus bus) {
        this.bus = bus;
    }

    @Override
    public BusInspectionSnapshot getSnapshot() {
        TopologySnapshot topology = bus.getTopology().getSnapshot();
        Map<String, TopologySnapshot.Message> messagesById = topology.messages().stream()
                .collect(Collectors.toMap(TopologySnapshot.Message::id, Function.identity()));
        Map<String, TopologySnapshot.Consumer> consumersById = topology.consumers().stream()
                .collect(Collectors.toMap(TopologySnapshot.Consumer::id, Function.identity()));
        Map<String, TopologySnapshot.Binding> bindingsById = topology.bindings().stream()
                .collect(Collectors.toMap(TopologySnapshot.Binding::id, Function.identity()));
        Map<String, TopologySnapshot.ReceiveEndpoint> endpointsById = topology.receiveEndpoints().stream()
                .collect(Collectors.toMap(TopologySnapshot.ReceiveEndpoint::id, Function.identity()));

        var messages = topology.messages().stream()
                .map(message -> new BusInspectionSnapshot.MessageInspection(
                        message.type(),
                        message.messageUrn(),
                        message.entityName(),
                        message.implementedMessageUrns(),
                        Map.of()))
                .toList();

        var consumers = topology.consumers().stream()
                .map(consumer -> new BusInspectionSnapshot.ConsumerInspection(
                        consumer.type(),
                        endpointsById.get(consumer.endpointId()).name(),
                        null,
                        null,
                        Map.of()))
                .toList();

        var endpoints = topology.receiveEndpoints().stream()
                .map(endpoint -> new BusInspectionSnapshot.ReceiveEndpointInspection(
                        endpoint.name(),
                        endpoint.logicalAddress(),
                        endpoint.bindingIds().stream()
                                .map(bindingsById::get)
                                .map(binding -> {
                                    TopologySnapshot.Message message = messagesById.get(binding.messageId());
                                    return new BusInspectionSnapshot.MessageBindingInspection(
                                            message.type(),
                                            message.messageUrn(),
                                            binding.entityName(),
                                            Map.of());
                                })
                                .toList(),
                        endpoint.consumerIds().stream().map(consumersById::get).map(TopologySnapshot.Consumer::type).toList(),
                        null,
                        Map.of()))
                .toList();

        return new BusInspectionSnapshot(
                bus.getAddress().getScheme(),
                bus.getAddress(),
                Instant.now(),
                messages,
                endpoints,
                consumers);
    }
}
