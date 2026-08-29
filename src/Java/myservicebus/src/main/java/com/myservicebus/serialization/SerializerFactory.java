package com.myservicebus.serialization;

public interface SerializerFactory {
    String getContentType();

    MessageSerializer createSerializer();

    MessageDeserializer createDeserializer();
}
