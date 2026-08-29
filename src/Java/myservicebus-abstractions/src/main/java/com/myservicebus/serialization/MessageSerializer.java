package com.myservicebus.serialization;

public interface MessageSerializer {
    String getContentType();

    <T> MessageBody getMessageBody(MessageSerializationContext<T> context) throws Exception;
}
