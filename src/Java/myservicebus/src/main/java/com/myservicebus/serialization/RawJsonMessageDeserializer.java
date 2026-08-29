package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;
import java.nio.charset.StandardCharsets;
import java.util.Map;

public final class RawJsonMessageDeserializer implements MessageDeserializer {
    private final ObjectMapper mapper;
    private final MessageHeaderConvention headerConvention;

    public RawJsonMessageDeserializer() {
        this(JsonSerializationDefaults.createObjectMapper(), MassTransitHeaderConvention.INSTANCE);
    }

    public RawJsonMessageDeserializer(MessageHeaderConvention headerConvention) {
        this(JsonSerializationDefaults.createObjectMapper(), headerConvention);
    }

    public RawJsonMessageDeserializer(ObjectMapper mapper) {
        this(mapper, MassTransitHeaderConvention.INSTANCE);
    }

    public RawJsonMessageDeserializer(ObjectMapper mapper, MessageHeaderConvention headerConvention) {
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
    public InboundMessage deserialize(MessageBody body, Map<String, Object> headers) {
        return new RawJsonInboundMessage(body.getBytes(), headers, mapper, headerConvention);
    }

    @Override
    public MessageBody getMessageBody(String text) {
        return new ByteArrayMessageBody(text.getBytes(StandardCharsets.UTF_8));
    }
}
