package com.myservicebus.topology;

import java.util.ArrayList;
import java.util.List;
import java.util.function.Consumer;

import com.myservicebus.ConsumeContext;
import com.myservicebus.EntityNameFormatter;
import com.myservicebus.PipeConfigurator;

public class TopologyRegistry implements BusTopology {
    private final List<MessageTopology> messages = new ArrayList<>();
    private final List<ConsumerTopology> consumers = new ArrayList<>();
    private final List<ReceiveEndpointDefinition> receiveEndpoints = new ArrayList<>();

    @Override
    public List<MessageTopology> getMessages() {
        return messages;
    }

    @Override
    public List<ConsumerTopology> getConsumers() {
        return consumers;
    }

    @Override
    public List<ReceiveEndpointDefinition> getReceiveEndpoints() {
        return List.copyOf(receiveEndpoints);
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
        topology.setEntityName(EntityNameFormatter.format(messageType));
        messages.add(topology);
        return topology;
    }

    public <TConsumer> void registerConsumer(Class<TConsumer> consumerType, String queueName, Consumer<PipeConfigurator<ConsumeContext<Object>>> configure, Class<?>... messageTypes) {
        ensureReceiveEndpoint(queueName);
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
        consumer.setConfigure(configure);
        consumers.add(consumer);
    }

    public void moveConsumerToEndpoint(ConsumerTopology consumer, String endpointName) {
        if (consumer == null) {
            throw new IllegalArgumentException("consumer must not be null");
        }
        if (endpointName == null || endpointName.isBlank()) {
            throw new IllegalArgumentException("endpointName must not be blank");
        }

        String previousEndpointName = consumer.getQueueName();
        consumer.setQueueName(endpointName);
        ensureReceiveEndpoint(endpointName);

        if (!previousEndpointName.equals(endpointName)
                && consumers.stream().noneMatch(x -> x != consumer && x.getQueueName().equals(previousEndpointName))) {
            receiveEndpoints.removeIf(x -> x.name().equals(previousEndpointName));
        }
    }

    private void ensureReceiveEndpoint(String endpointName) {
        boolean exists = receiveEndpoints.stream().anyMatch(x -> x.name().equals(endpointName));
        if (!exists) {
            receiveEndpoints.add(new ReceiveEndpointDefinition(endpointName, true, false));
        }
    }
}
