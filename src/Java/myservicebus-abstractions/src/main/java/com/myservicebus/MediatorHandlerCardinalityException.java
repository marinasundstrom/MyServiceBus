package com.myservicebus;

import java.util.List;
import java.util.stream.Collectors;

/** Thrown when mediator send finds more than one compatible handler. */
public final class MediatorHandlerCardinalityException extends IllegalStateException {
    private final Class<?> messageType;
    private final List<Class<?>> handlerTypes;

    public MediatorHandlerCardinalityException(Class<?> messageType, List<Class<?>> handlerTypes) {
        super("Mediator send requires exactly one handler for '" + messageType.getName()
                + "', but found " + handlerTypes.size() + ": "
                + handlerTypes.stream().map(Class::getName).collect(Collectors.joining(", ")) + ".");
        this.messageType = messageType;
        this.handlerTypes = List.copyOf(handlerTypes);
    }

    public Class<?> getMessageType() {
        return messageType;
    }

    public List<Class<?>> getHandlerTypes() {
        return handlerTypes;
    }
}
