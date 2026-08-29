package com.myservicebus.serialization;

public final class EnvelopeSerializerFactory implements SerializerFactory {
    private final MessageHeaderConvention headerConvention;

    public EnvelopeSerializerFactory() {
        this(MassTransitHeaderConvention.INSTANCE);
    }

    public EnvelopeSerializerFactory(MessageHeaderConvention headerConvention) {
        this.headerConvention = headerConvention;
    }

    @Override
    public String getContentType() {
        return DefaultInboundMessageResolver.ENVELOPE_CONTENT_TYPE;
    }

    @Override
    public MessageSerializer createSerializer() {
        return new EnvelopeMessageSerializer(headerConvention);
    }

    @Override
    public MessageDeserializer createDeserializer() {
        return new EnvelopeMessageDeserializer(headerConvention);
    }
}
