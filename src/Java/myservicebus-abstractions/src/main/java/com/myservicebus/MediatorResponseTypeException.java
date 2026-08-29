package com.myservicebus;

/** Thrown when a mediator result handler cannot produce the requested response type. */
public final class MediatorResponseTypeException extends IllegalStateException {
    public MediatorResponseTypeException(Class<?> messageType, Class<?> responseType, Class<?> handlerType) {
        super(responseType == Void.class
                ? "Mediator handler '" + handlerType.getName() + "' produces a response for '"
                        + messageType.getName() + "'. Use send(message, responseType) instead."
                : "Mediator handler '" + handlerType.getName() + "' cannot produce response type '"
                        + responseType.getName() + "' for '" + messageType.getName() + "'.");
    }
}
