package com.myservicebus.serialization;

/** A supported message payload could not be materialized for consumption. */
public final class MessageDeserializationException extends java.io.IOException {
    public MessageDeserializationException(String message) { super(message); }
    public MessageDeserializationException(String message, Throwable cause) { super(message, cause); }
}
