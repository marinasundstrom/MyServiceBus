package com.myservicebus;

/** Thrown when mediator send cannot find a compatible handler. */
public final class MediatorHandlerNotFoundException extends IllegalStateException {
    private final Class<?> messageType;

    public MediatorHandlerNotFoundException(Class<?> messageType) {
        super("No mediator handler is registered for message type '" + messageType.getName() + "'.");
        this.messageType = messageType;
    }

    public Class<?> getMessageType() {
        return messageType;
    }
}
