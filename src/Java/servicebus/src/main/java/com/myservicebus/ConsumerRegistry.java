package com.myservicebus;

import java.util.ArrayList;
import java.util.List;

public class ConsumerRegistry {
    private final List<ConsumerDefinition<?, ?>> definitions = new ArrayList<>();

    public <TConsumer, TMessage> void register(Class<TConsumer> consumer, Class<TMessage> messageType) {
        definitions.add(new ConsumerDefinition<>(consumer, messageType));
    }

    public List<ConsumerDefinition<?, ?>> getAll() {
        return definitions;
    }
}