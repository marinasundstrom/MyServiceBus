package com.myservicebus.topology;

import java.util.List;

public record TopologySnapshot(
        int version,
        List<Message> messages,
        List<ReceiveEndpoint> receiveEndpoints,
        List<Consumer> consumers,
        List<Binding> bindings) {

    public static final int CURRENT_VERSION = 1;

    public TopologySnapshot {
        messages = List.copyOf(messages);
        receiveEndpoints = List.copyOf(receiveEndpoints);
        consumers = List.copyOf(consumers);
        bindings = List.copyOf(bindings);
    }

    public record Message(
            String id,
            String type,
            String messageUrn,
            String entityName,
            List<String> implementedMessageUrns) {
        public Message {
            implementedMessageUrns = List.copyOf(implementedMessageUrns);
        }
    }

    public record ReceiveEndpoint(
            String id,
            String name,
            String logicalAddress,
            boolean durable,
            boolean temporary,
            List<String> consumerIds,
            List<String> bindingIds) {
        public ReceiveEndpoint {
            consumerIds = List.copyOf(consumerIds);
            bindingIds = List.copyOf(bindingIds);
        }
    }

    public record Consumer(
            String id,
            String type,
            String endpointId,
            List<String> messageIds) {
        public Consumer {
            messageIds = List.copyOf(messageIds);
        }
    }

    public record Binding(
            String id,
            String endpointId,
            String messageId,
            String entityName,
            String kind) {
    }
}
