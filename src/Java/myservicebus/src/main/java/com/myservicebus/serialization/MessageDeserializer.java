package com.myservicebus.serialization;

import java.util.Map;

public interface MessageDeserializer {
    String getContentType();

    MessageEnvelopeMode getEnvelopeMode();

    InboundMessage deserialize(MessageBody body, Map<String, Object> headers) throws Exception;

    MessageBody getMessageBody(String text);
}
