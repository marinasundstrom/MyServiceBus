package com.myservicebus.serialization;

import java.lang.reflect.Type;
import com.myservicebus.Envelope;

public interface MessageDeserializer {
    <T> Envelope<T> deserialize(byte[] data, Type type) throws Exception;
}
