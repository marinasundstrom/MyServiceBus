package com.myservicebus.serialization;

public interface MessageSerializer {
    String getContentType();

    MessageEnvelopeMode getEnvelopeMode();

    <T> byte[] serialize(MessageSerializationContext<T> context) throws Exception;
}
