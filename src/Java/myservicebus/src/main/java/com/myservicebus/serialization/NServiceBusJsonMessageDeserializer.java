package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;
import java.nio.charset.StandardCharsets;
import java.util.Map;

public final class NServiceBusJsonMessageDeserializer implements MessageDeserializer {
    private final ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();

    @Override
    public String getContentType() {
        return DefaultInboundMessageResolver.RAW_JSON_CONTENT_TYPE;
    }

    @Override
    public InboundMessage deserialize(MessageBody body, Map<String, Object> headers) {
        return new NServiceBusJsonInboundMessage(body.getBytes(), headers, mapper);
    }

    @Override
    public MessageBody getMessageBody(String text) {
        return new ByteArrayMessageBody(text.getBytes(StandardCharsets.UTF_8));
    }
}
