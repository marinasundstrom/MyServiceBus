package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;

public class RawJsonMessageSerializer implements MessageSerializer {
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
        this(MassTransitHeaderConvention.INSTANCE);
    }

    public RawJsonMessageSerializer(MessageHeaderConvention headerConvention) {
        this.headerConvention = headerConvention;
        this.mapper = new ObjectMapper();
        this.mapper.findAndRegisterModules();
    }

    @Override
    public <T> byte[] serialize(MessageSerializationContext<T> context) throws IOException {
        context.getHeaders().put(headerConvention.getContentTypeHeader(), getContentType());
        return mapper.writeValueAsBytes(context.getMessage());
    }
}
