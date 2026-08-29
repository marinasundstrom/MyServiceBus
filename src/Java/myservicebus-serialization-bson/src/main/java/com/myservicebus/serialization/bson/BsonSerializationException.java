package com.myservicebus.serialization.bson;

/**
 * The MassTransit BSON envelope could not be serialized or deserialized.
 */
public final class BsonSerializationException extends RuntimeException {
    public BsonSerializationException(String message, Throwable cause) {
        super(message, cause);
    }
}
