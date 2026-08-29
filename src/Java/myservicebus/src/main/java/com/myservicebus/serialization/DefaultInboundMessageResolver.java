package com.myservicebus.serialization;

import java.nio.charset.StandardCharsets;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

import com.myservicebus.TransportMessage;

public class DefaultInboundMessageResolver implements InboundMessageResolver {
    public static final String ENVELOPE_CONTENT_TYPE = "application/vnd.masstransit+json";
    public static final String RAW_JSON_CONTENT_TYPE = "application/json";

    private final Map<String, MessageDeserializer> deserializers;
    private final MessageDeserializer nServiceBusDeserializer;
    private final MessageHeaderConvention headerConvention;
    private final String defaultContentType;

    public DefaultInboundMessageResolver(MessageDeserializer envelopeDeserializer) {
        this(List.of(
                envelopeDeserializer,
                new RawJsonMessageDeserializer(),
                new NServiceBusJsonMessageDeserializer()),
                ENVELOPE_CONTENT_TYPE,
                MassTransitHeaderConvention.INSTANCE);
    }

    public DefaultInboundMessageResolver(MessageDeserializer envelopeDeserializer, MessageHeaderConvention headerConvention) {
        this(List.of(
                envelopeDeserializer,
                new RawJsonMessageDeserializer(headerConvention),
                new NServiceBusJsonMessageDeserializer()),
                ENVELOPE_CONTENT_TYPE,
                headerConvention);
    }

    public DefaultInboundMessageResolver(
            List<MessageDeserializer> deserializers,
            String defaultContentType,
            MessageHeaderConvention headerConvention) {
        if (deserializers == null || deserializers.isEmpty()) {
            throw new IllegalArgumentException("deserializers must not be empty");
        }
        if (defaultContentType == null || defaultContentType.isBlank()) {
            throw new IllegalArgumentException("defaultContentType must not be blank");
        }
        this.headerConvention = headerConvention;
        this.defaultContentType = defaultContentType;
        this.deserializers = new LinkedHashMap<>();
        MessageDeserializer nServiceBus = null;
        for (MessageDeserializer deserializer : deserializers) {
            if (deserializer instanceof NServiceBusJsonMessageDeserializer) {
                nServiceBus = deserializer;
            } else {
                this.deserializers.put(normalize(deserializer.getContentType()), deserializer);
            }
        }
        this.nServiceBusDeserializer = nServiceBus;
    }

    @Override
    public InboundMessage resolve(TransportMessage transportMessage) throws Exception {
        if (transportMessage.getHeaders().containsKey(NServiceBusHeaders.ENCLOSED_MESSAGE_TYPES)
                || transportMessage.getHeaders().containsKey(NServiceBusHeaders.CONTENT_TYPE)) {
            if (nServiceBusDeserializer != null) {
                return nServiceBusDeserializer.deserialize(
                        new ByteArrayMessageBody(transportMessage.getBody()),
                        transportMessage.getHeaders());
            }
        }

        String contentType = readContentType(transportMessage);
        MessageDeserializer deserializer = deserializers.get(normalize(contentType));
        if (deserializer != null) {
            return deserializer.deserialize(
                    new ByteArrayMessageBody(transportMessage.getBody()),
                    transportMessage.getHeaders());
        }

        throw new IllegalArgumentException("Invalid Content Type: " + contentType);
    }

    private String readContentType(TransportMessage transportMessage) {
        Object value = transportMessage.getHeaders().get(headerConvention.getContentTypeHeader());
        if (value instanceof byte[] bytes) {
            return new String(bytes, StandardCharsets.UTF_8);
        }

        return value != null ? value.toString() : defaultContentType;
    }

    private static String normalize(String contentType) {
        return contentType.toLowerCase(Locale.ROOT);
    }
}
