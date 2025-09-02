package com.myservicebus;

import java.util.ArrayList;
import java.util.List;

public class ConsumerRegistry {
    private final List<ConsumerDefinition<?, ?>> definitions = new ArrayList<>();

    public <TConsumer, TMessage> void register(Class<TConsumer> consumer, Class<TMessage> messageType) {
        register(consumer, messageType, null, null);
    }

    public <TConsumer, TMessage> void register(Class<TConsumer> consumer, Class<TMessage> messageType, String queueName,
            String exchangeName) {
        // Check if a definition already exists for this consumer/message pair
        for (ConsumerDefinition<?, ?> def : definitions) {
            if (def.getConsumerType().equals(consumer) && def.getMessageType().equals(messageType)) {
                def.setQueueName(queueName);
                def.setExchangeName(exchangeName);
                return;
            }
        }
        definitions.add(new ConsumerDefinition<>(consumer, messageType, queueName, exchangeName));
    }

    public List<ConsumerDefinition<?, ?>> getAll() {
        return definitions;
    }
}