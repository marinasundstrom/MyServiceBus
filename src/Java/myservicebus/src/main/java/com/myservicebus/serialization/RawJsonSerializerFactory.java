package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;

public final class RawJsonSerializerFactory implements SerializerFactory {
    private final MessageHeaderConvention headerConvention;
    private final ObjectMapper mapper;

    public RawJsonSerializerFactory() {
        this(JsonSerializationDefaults.createObjectMapper(), MassTransitHeaderConvention.INSTANCE);
    }

    public RawJsonSerializerFactory(MessageHeaderConvention headerConvention) {
        this(JsonSerializationDefaults.createObjectMapper(), headerConvention);
    }

    public RawJsonSerializerFactory(ObjectMapper mapper) {
        this(mapper, MassTransitHeaderConvention.INSTANCE);
    }

    public RawJsonSerializerFactory(ObjectMapper mapper, MessageHeaderConvention headerConvention) {
        if (mapper == null) {
            throw new IllegalArgumentException("mapper must not be null");
        }
        this.mapper = mapper;
        this.headerConvention = headerConvention;
    }

    @Override
    public String getContentType() {
        return DefaultInboundMessageResolver.RAW_JSON_CONTENT_TYPE;
    }

    @Override
    public MessageSerializer createSerializer() {
        return new RawJsonMessageSerializer(mapper, headerConvention);
    }

    @Override
    public MessageDeserializer createDeserializer() {
        return new RawJsonMessageDeserializer(mapper, headerConvention);
    }
}
