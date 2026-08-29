package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;

public class RawJsonMessageSerializer implements MessageSerializer, MessageSerializerMetadata {
    private final ObjectMapper mapper;
    private final MessageHeaderConvention headerConvention;

    @Override
    public String getContentType() {
        return DefaultInboundMessageResolver.RAW_JSON_CONTENT_TYPE;
    }

    @Override
    public MessageEnvelopeMode getEnvelopeMode() {
        return MessageEnvelopeMode.RAW;
    }

    public RawJsonMessageSerializer() {
        this(JsonSerializationDefaults.createObjectMapper(), MassTransitHeaderConvention.INSTANCE);
    }

    public RawJsonMessageSerializer(MessageHeaderConvention headerConvention) {
        this(JsonSerializationDefaults.createObjectMapper(), headerConvention);
    }

    public RawJsonMessageSerializer(ObjectMapper mapper) {
        this(mapper, MassTransitHeaderConvention.INSTANCE);
    }

    public RawJsonMessageSerializer(ObjectMapper mapper, MessageHeaderConvention headerConvention) {
        if (mapper == null) {
            throw new IllegalArgumentException("mapper must not be null");
        }
        this.mapper = mapper;
        this.headerConvention = headerConvention;
    }

    @Override
    public <T> MessageBody getMessageBody(MessageSerializationContext<T> context) throws IOException {
        context.getHeaders().put(headerConvention.getContentTypeHeader(), getContentType());
        return new ByteArrayMessageBody(mapper.writeValueAsBytes(context.getMessage()));
    }
}
