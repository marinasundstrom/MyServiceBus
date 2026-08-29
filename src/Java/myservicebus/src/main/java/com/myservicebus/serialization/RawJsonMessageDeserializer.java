package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;
import java.nio.charset.StandardCharsets;
import java.util.Map;

public final class RawJsonMessageDeserializer implements MessageDeserializer {
    private final ObjectMapper mapper;
    private final MessageHeaderConvention headerConvention;

    public RawJsonMessageDeserializer() {
        this(MassTransitHeaderConvention.INSTANCE);
    }

    public RawJsonMessageDeserializer(MessageHeaderConvention headerConvention) {
        this.headerConvention = headerConvention;
        mapper = new ObjectMapper();
        mapper.findAndRegisterModules();
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
