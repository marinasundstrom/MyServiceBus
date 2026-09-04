package com.myservicebus.topology;

import java.util.ArrayList;
import java.util.List;
import java.util.function.Consumer;

import com.myservicebus.ConsumeContext;
import com.myservicebus.ConsumerDefinition;
import com.myservicebus.ConsumerMethodInvoker;
import com.myservicebus.EntityNameFormatter;
import com.myservicebus.PipeConfigurator;
import com.myservicebus.choreography.ChoreographyFragment;
import com.myservicebus.orchestration.SagaStateMachineDefinition;

public class TopologyRegistry implements BusTopology {
    private final List<MessageTopology> messages = new ArrayList<>();
    private final List<ConsumerTopology> consumers = new ArrayList<>();
    private final List<ConsumerDefinitionModel> consumerDefinitions = new ArrayList<>();
    private final List<ReceiveEndpointDefinition> receiveEndpoints = new ArrayList<>();
    private final List<ChoreographyFragment> choreographies = new ArrayList<>();
    private final List<SagaStateMachineTopology> sagaStateMachines = new ArrayList<>();

    @Override
    public List<MessageTopology> getMessages() {
        return messages;
    }

    @Override
    public List<ConsumerTopology> getConsumers() {
        return consumers;
    }

    @Override
    public List<ConsumerDefinitionModel> getConsumerDefinitions() {
        return java.util.Collections.unmodifiableList(consumerDefinitions);
    }

    @Override
    public List<ReceiveEndpointDefinition> getReceiveEndpoints() {
        return List.copyOf(receiveEndpoints);
    }

    @Override
    public List<ChoreographyFragment> getChoreographies() {
        return List.copyOf(choreographies);
    }

    @Override
    public List<SagaStateMachineTopology> getSagaStateMachines() {
        return List.copyOf(sagaStateMachines);
    }

    public void registerSagaStateMachine(SagaStateMachineDefinition definition, String endpointName) {
        if (definition == null) {
            throw new IllegalArgumentException("definition must not be null");
        }
        if (endpointName == null || endpointName.isBlank()) {
            throw new IllegalArgumentException("endpointName must not be blank");
        }
        definition.validate();
        boolean duplicate = sagaStateMachines.stream().anyMatch(existing ->
                existing.definition().stateMachineId().equals(definition.stateMachineId())
                        && existing.definition().owner().equals(definition.owner()));
        if (duplicate) {
            throw new IllegalArgumentException(
                    "Saga state machine '" + definition.stateMachineId()
                            + "' is already registered by '" + definition.owner() + "'.");
        }
        sagaStateMachines.add(new SagaStateMachineTopology(definition, endpointName));
    }

    public void registerChoreography(ChoreographyFragment fragment) {
        if (fragment == null) {
            throw new IllegalArgumentException("fragment must not be null");
        }
        fragment.validate();
        boolean duplicate = choreographies.stream().anyMatch(existing ->
                existing.choreographyId().equals(fragment.choreographyId())
                        && existing.owner().equals(fragment.owner()));
        if (duplicate) {
            throw new IllegalArgumentException(
                    "Choreography '" + fragment.choreographyId()
                            + "' already has a fragment owned by '" + fragment.owner() + "'.");
        }
        choreographies.add(fragment);
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
        registerConsumer(consumerType, queueName, false, consumerType, configure, messageTypes);
    }

    public <TConsumer> void registerConsumer(
            Class<TConsumer> consumerType,
            String queueName,
            boolean endpointNameExplicit,
            Class<?> endpointNameFormatterType,
            Consumer<PipeConfigurator<ConsumeContext<Object>>> configure,
            Class<?>... messageTypes) {
        registerConsumerCore(
                consumerType,
                queueName,
                endpointNameExplicit,
                endpointNameFormatterType,
                configure,
                null,
                messageTypes);
    }

    public <TConsumer> ConsumerDefinitionModel registerConsumerDefinition(
            Class<TConsumer> consumerType,
            String queueName,
            boolean endpointNameExplicit,
            Class<?> endpointNameFormatterType,
            Consumer<PipeConfigurator<ConsumeContext<Object>>> configure,
            ConsumerDefinition<TConsumer> definition,
            Class<?>... messageTypes) {
        return registerConsumerCore(
                consumerType,
                queueName,
                endpointNameExplicit,
                endpointNameFormatterType,
                configure,
                definition,
                messageTypes);
    }

    private <TConsumer> ConsumerDefinitionModel registerConsumerCore(
            Class<TConsumer> consumerType,
            String queueName,
            boolean endpointNameExplicit,
            Class<?> endpointNameFormatterType,
            Consumer<PipeConfigurator<ConsumeContext<Object>>> configure,
            ConsumerDefinition<TConsumer> definition,
            Class<?>... messageTypes) {
        ConsumerDefinitionModel model = new ConsumerDefinitionModel(
                consumerType,
                queueName,
                endpointNameExplicit,
                endpointNameFormatterType,
                java.util.Arrays.asList(messageTypes),
                definition != null ? definition.getConcurrentMessageLimit() : null);
        consumerDefinitions.add(model);
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
        consumer.setDefinition(model);
        consumer.setConsumerType(consumerType);
        consumer.setQueueName(queueName);
        consumer.setEndpointNameExplicit(endpointNameExplicit);
        consumer.setEndpointNameFormatterType(endpointNameFormatterType);
        consumer.setBindings(bindings);
        consumer.setConfigure(configure);
        if (definition != null) {
            consumer.setConcurrentMessageLimit(definition.getConcurrentMessageLimit());
        }
        consumers.add(consumer);
        return model;
    }

    public <TMessage> void registerConsumerMethod(
            Class<?> declaringType,
            Class<TMessage> messageType,
            String queueName,
            ConsumerMethodInvoker<TMessage> invoker) {
        registerConsumerMethod(declaringType, messageType, queueName, true, null, invoker);
    }

    public <TMessage> void registerConsumerMethod(
            Class<?> declaringType,
            Class<TMessage> messageType,
            String queueName,
            boolean endpointNameExplicit,
            Class<?> endpointNameFormatterType,
            ConsumerMethodInvoker<TMessage> invoker) {
        registerConsumerCore(
                declaringType,
                queueName,
                endpointNameExplicit,
                endpointNameFormatterType,
                null,
                null,
                messageType);
        consumers.get(consumers.size() - 1).setMethodInvoker(invoker);
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
