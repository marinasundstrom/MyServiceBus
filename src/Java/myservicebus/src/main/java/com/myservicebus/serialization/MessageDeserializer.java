package com.myservicebus.serialization;

import com.myservicebus.Envelope;

public interface MessageDeserializer {
    <T> Envelope<T> deserialize(byte[] data, Class<T> clazz) throws Exception;
}
