package com.myservicebus.serialization;

public interface MessageSerializer {
    <T> byte[] serialize(MessageSerializationContext<T> context) throws Exception;
}
