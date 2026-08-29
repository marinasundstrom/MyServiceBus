package com.myservicebus.serialization;

public final class NServiceBusJsonSerializerFactory implements SerializerFactory {
    @Override
    public String getContentType() {
        return DefaultInboundMessageResolver.RAW_JSON_CONTENT_TYPE;
    }

    @Override
    public MessageSerializer createSerializer() {
        return new NServiceBusJsonMessageSerializer();
    }

    @Override
    public MessageDeserializer createDeserializer() {
        return new NServiceBusJsonMessageDeserializer();
    }
}
