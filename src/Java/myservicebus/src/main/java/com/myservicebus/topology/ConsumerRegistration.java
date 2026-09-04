package com.myservicebus.topology;

import com.myservicebus.ConsumerInvoker;

/**
 * A source-language-neutral consumer registration ready for JVM topology
 * materialization.
 *
 * @param definition normalized identity, endpoint policy, and message contracts
 * @param messageType message contract handled by this invocation
 * @param invoker adapter from the shared JVM runtime to a language projection
 */
public record ConsumerRegistration<TMessage>(
        ConsumerDefinitionModel definition,
        Class<TMessage> messageType,
        ConsumerInvoker<TMessage> invoker) {
    public ConsumerRegistration {
        if (definition == null) {
            throw new IllegalArgumentException("definition must not be null");
        }
        if (messageType == null) {
            throw new IllegalArgumentException("messageType must not be null");
        }
        if (invoker == null) {
            throw new IllegalArgumentException("invoker must not be null");
        }
        if (!definition.messageTypes().contains(messageType)) {
            throw new IllegalArgumentException("messageType must be included in the consumer definition");
        }
    }
}
