package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;

public final class EnvelopeSerializerFactory implements SerializerFactory {
    private final MessageHeaderConvention headerConvention;
    private final ObjectMapper mapper;

    public EnvelopeSerializerFactory() {
        this(JsonSerializationDefaults.createObjectMapper(), MassTransitHeaderConvention.INSTANCE);
    }

    public EnvelopeSerializerFactory(MessageHeaderConvention headerConvention) {
        this(JsonSerializationDefaults.createObjectMapper(), headerConvention);
    }

    public EnvelopeSerializerFactory(ObjectMapper mapper) {
        this(mapper, MassTransitHeaderConvention.INSTANCE);
    }

    public EnvelopeSerializerFactory(ObjectMapper mapper, MessageHeaderConvention headerConvention) {
        if (mapper == null) {
            throw new IllegalArgumentException("mapper must not be null");
        }
        this.mapper = mapper;
        this.headerConvention = headerConvention;
    }

    @Override
    public String getContentType() {
        return DefaultInboundMessageResolver.ENVELOPE_CONTENT_TYPE;
    }

    @Override
    public MessageSerializer createSerializer() {
        return new EnvelopeMessageSerializer(mapper, headerConvention);
    }

    @Override
    public MessageDeserializer createDeserializer() {
        return new EnvelopeMessageDeserializer(mapper, headerConvention);
    }
}
