package com.myservicebus.serialization;

public final class RawJsonSerializerFactory implements SerializerFactory {
    private final MessageHeaderConvention headerConvention;

    public RawJsonSerializerFactory() {
        this(MassTransitHeaderConvention.INSTANCE);
    }

    public RawJsonSerializerFactory(MessageHeaderConvention headerConvention) {
        this.headerConvention = headerConvention;
    }

    @Override
    public String getContentType() {
        return DefaultInboundMessageResolver.RAW_JSON_CONTENT_TYPE;
    }

    @Override
    public MessageSerializer createSerializer() {
        return new RawJsonMessageSerializer(headerConvention);
    }

    @Override
    public MessageDeserializer createDeserializer() {
        return new RawJsonMessageDeserializer(headerConvention);
    }
}
