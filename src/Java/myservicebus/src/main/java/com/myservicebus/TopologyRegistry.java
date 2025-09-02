package com.myservicebus;

import java.util.ArrayList;
import java.util.List;

public class TopologyRegistry {
    private final List<MessageTopology> messages = new ArrayList<>();
    private final List<ConsumerTopology> consumers = new ArrayList<>();

    public List<MessageTopology> getMessages() {
        return messages;
    }

    public List<ConsumerTopology> getConsumers() {
        return consumers;
    }

    public <T> void registerMessage(Class<T> messageType, String entityName) {
        MessageTopology topology = new MessageTopology();
        topology.setMessageType(messageType);
        topology.setEntityName(entityName);
        messages.add(topology);
    }

    private MessageTopology registerMessage(Class<?> messageType) {
        MessageTopology topology = new MessageTopology();
        topology.setMessageType(messageType);
        topology.setEntityName(NamingConventions.getExchangeName(messageType));
        messages.add(topology);
        return topology;
    }

    public <TConsumer> void registerConsumer(Class<TConsumer> consumerType, String queueName, Class<?>... messageTypes) {
        List<MessageBinding> bindings = new ArrayList<>();
        for (Class<?> mt : messageTypes) {
            MessageTopology msg = messages.stream()
                    .filter(m -> m.getMessageType().equals(mt))
                    .findFirst()
                    .orElseGet(() -> registerMessage(mt));
            MessageBinding binding = new MessageBinding();
            binding.setMessageType(mt);
            binding.setEntityName(msg.getEntityName());
            bindings.add(binding);
        }
        ConsumerTopology consumer = new ConsumerTopology();
        consumer.setConsumerType(consumerType);
        consumer.setQueueName(queueName);
        consumer.setBindings(bindings);
        consumers.add(consumer);
    }
}
