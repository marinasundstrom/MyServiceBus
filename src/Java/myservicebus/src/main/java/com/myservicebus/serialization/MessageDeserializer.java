package com.myservicebus.serialization;

import java.util.Map;

public interface MessageDeserializer {
    String getContentType();

    InboundMessage deserialize(MessageBody body, Map<String, Object> headers) throws Exception;

    MessageBody getMessageBody(String text);
}
