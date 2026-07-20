package com.myservicebus.topology;

import com.myservicebus.MessageUrn;

import java.util.Comparator;
import java.util.List;
import java.util.function.Function;
import java.util.stream.Collectors;

final class TopologySnapshots {
    private TopologySnapshots() {
    }

    static TopologySnapshot create(BusTopology topology) {
        List<TopologySnapshot.Message> messages = topology.getMessages().stream()
                .collect(Collectors.toMap(
                        message -> MessageUrn.forClass(message.getMessageType()),
                        Function.identity(),
                        (left, right) -> left))
                .values().stream()
                .map(message -> new TopologySnapshot.Message(
                        MessageUrn.forClass(message.getMessageType()),
                        message.getMessageType().getName(),
                        MessageUrn.forClass(message.getMessageType()),
                        message.getEntityName(),
                        List.of(message.getMessageType().getInterfaces()).stream()
                                .map(MessageUrn::forClass)
                                .distinct()
                                .sorted()
                                .toList()))
                .sorted(Comparator.comparing(TopologySnapshot.Message::id))
                .toList();

        List<TopologySnapshot.Consumer> consumers = topology.getConsumers().stream()
                .map(consumer -> {
                    String endpointId = endpointId(consumer.getQueueName());
                    String consumerType = consumer.getConsumerType().getName();
                    return new TopologySnapshot.Consumer(
                            endpointId + "|consumer:" + consumerType,
                            consumerType,
                            endpointId,
                            consumer.getBindings().stream()
                                    .map(binding -> MessageUrn.forClass(binding.getMessageType()))
                                    .distinct()
                                    .sorted()
                                    .toList());
                })
                .collect(Collectors.toMap(
                        TopologySnapshot.Consumer::id,
                        Function.identity(),
                        (left, right) -> left))
                .values().stream()
                .sorted(Comparator.comparing(TopologySnapshot.Consumer::id))
                .toList();

        List<TopologySnapshot.Binding> bindings = topology.getConsumers().stream()
                .flatMap(consumer -> consumer.getBindings().stream().map(binding -> {
                    String endpointId = endpointId(consumer.getQueueName());
                    String messageId = MessageUrn.forClass(binding.getMessageType());
                    return new TopologySnapshot.Binding(
                            endpointId + "|binding:" + messageId + "|" + binding.getEntityName(),
                            endpointId,
                            messageId,
                            binding.getEntityName(),
                            "publish");
                }))
                .collect(Collectors.toMap(
                        TopologySnapshot.Binding::id,
                        Function.identity(),
                        (left, right) -> left))
                .values().stream()
                .sorted(Comparator.comparing(TopologySnapshot.Binding::id))
                .toList();

        List<TopologySnapshot.ReceiveEndpoint> endpoints = topology.getReceiveEndpoints().stream()
                .map(endpoint -> {
                    String id = endpointId(endpoint.name());
                    return new TopologySnapshot.ReceiveEndpoint(
                            id,
                            endpoint.name(),
                            "queue:" + endpoint.name(),
                            endpoint.durable(),
                            endpoint.temporary(),
                            consumers.stream().filter(x -> x.endpointId().equals(id)).map(TopologySnapshot.Consumer::id)
                                    .toList(),
                            bindings.stream().filter(x -> x.endpointId().equals(id)).map(TopologySnapshot.Binding::id)
                                    .toList());
                })
                .sorted(Comparator.comparing(TopologySnapshot.ReceiveEndpoint::id))
                .toList();

        return new TopologySnapshot(TopologySnapshot.CURRENT_VERSION, messages, endpoints, consumers, bindings);
    }

    private static String endpointId(String endpointName) {
        return "endpoint:" + endpointName;
    }
}
