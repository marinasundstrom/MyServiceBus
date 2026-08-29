package com.myservicebus.serialization;

import java.io.IOException;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.nio.charset.StandardCharsets;
import java.util.Map;

public class EnvelopeMessageDeserializer implements MessageDeserializer {
    private final ObjectMapper mapper;
    private final MessageHeaderConvention headerConvention;

    public EnvelopeMessageDeserializer() {
        this(JsonSerializationDefaults.createObjectMapper(), MassTransitHeaderConvention.INSTANCE);
    }

    public EnvelopeMessageDeserializer(MessageHeaderConvention headerConvention) {
        this(JsonSerializationDefaults.createObjectMapper(), headerConvention);
    }

    public EnvelopeMessageDeserializer(ObjectMapper mapper) {
        this(mapper, MassTransitHeaderConvention.INSTANCE);
    }

    public EnvelopeMessageDeserializer(ObjectMapper mapper, MessageHeaderConvention headerConvention) {
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
    public InboundMessage deserialize(MessageBody body, Map<String, Object> headers) throws IOException {
        return new EnvelopeInboundMessage(body.getBytes(), headers, mapper, headerConvention);
    }

    @Override
    public MessageBody getMessageBody(String text) {
        return new ByteArrayMessageBody(text.getBytes(StandardCharsets.UTF_8));
    }
}
