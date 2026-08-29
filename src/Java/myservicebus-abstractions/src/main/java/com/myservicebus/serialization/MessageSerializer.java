package com.myservicebus.serialization;

public interface MessageSerializer {
    String getContentType();

    MessageEnvelopeMode getEnvelopeMode();

    <T> MessageBody getMessageBody(MessageSerializationContext<T> context) throws Exception;
}
