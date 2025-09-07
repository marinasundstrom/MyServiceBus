package com.myservicebus;

public class UnknownMessageTypeException extends RuntimeException {
    public UnknownMessageTypeException(String messageType) {
        super("Unknown message type: " + (messageType != null ? messageType : "<null>"));
    }
}
