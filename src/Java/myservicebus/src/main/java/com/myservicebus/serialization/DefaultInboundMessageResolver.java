package com.myservicebus.serialization;

import java.nio.charset.StandardCharsets;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.TransportMessage;

public class DefaultInboundMessageResolver implements InboundMessageResolver {
    public static final String ENVELOPE_CONTENT_TYPE = "application/vnd.masstransit+json";
    public static final String RAW_JSON_CONTENT_TYPE = "application/json";

    private final MessageDeserializer envelopeDeserializer;
    private final ObjectMapper rawMessageMapper;
    private final MessageHeaderConvention headerConvention;

    public DefaultInboundMessageResolver(MessageDeserializer envelopeDeserializer) {
        this(envelopeDeserializer, MassTransitHeaderConvention.INSTANCE);
    }

    public DefaultInboundMessageResolver(MessageDeserializer envelopeDeserializer, MessageHeaderConvention headerConvention) {
        this.envelopeDeserializer = envelopeDeserializer;
        this.headerConvention = headerConvention;
        this.rawMessageMapper = new ObjectMapper();
        this.rawMessageMapper.findAndRegisterModules();
    }

    @Override
    public InboundMessage resolve(TransportMessage transportMessage) throws Exception {
        if (transportMessage.getHeaders().containsKey(NServiceBusHeaders.ENCLOSED_MESSAGE_TYPES)
                || transportMessage.getHeaders().containsKey(NServiceBusHeaders.CONTENT_TYPE)) {
            return new NServiceBusJsonInboundMessage(
                    transportMessage.getBody(), transportMessage.getHeaders(), rawMessageMapper);
        }

        String contentType = readContentType(transportMessage);
        if (RAW_JSON_CONTENT_TYPE.equalsIgnoreCase(contentType)) {
            return new RawJsonInboundMessage(transportMessage.getBody(), transportMessage.getHeaders(), rawMessageMapper, headerConvention);
        }

        if (ENVELOPE_CONTENT_TYPE.equalsIgnoreCase(contentType)) {
            return envelopeDeserializer.deserialize(
                    new ByteArrayMessageBody(transportMessage.getBody()), transportMessage.getHeaders());
        }

        throw new IllegalArgumentException("Invalid Content Type: " + contentType);
    }

    private String readContentType(TransportMessage transportMessage) {
        Object value = transportMessage.getHeaders().get(headerConvention.getContentTypeHeader());
        if (value instanceof byte[] bytes) {
            return new String(bytes, StandardCharsets.UTF_8);
        }

        return value != null ? value.toString() : ENVELOPE_CONTENT_TYPE;
    }
}
