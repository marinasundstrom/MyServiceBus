package com.myservicebus.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.MapperFeature;

import java.lang.reflect.Type;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

public final class NServiceBusJsonInboundMessage implements InboundMessage {
    private final byte[] body;
    private final Map<String, Object> headers;
    private final ObjectMapper mapper;
    private final Map<Type, Object> messageCache = new ConcurrentHashMap<>();

    public NServiceBusJsonInboundMessage(byte[] body, Map<String, Object> headers, ObjectMapper mapper) {
        this.body = body;
        this.headers = headers;
        this.mapper = mapper.copy().configure(MapperFeature.ACCEPT_CASE_INSENSITIVE_PROPERTIES, true);
    }

    @Override
    public InboundMessageFormat getFormat() {
        return InboundMessageFormat.NSERVICEBUS_JSON;
    }

    @Override
    public String getContentType() {
        return DefaultInboundMessageResolver.RAW_JSON_CONTENT_TYPE;
    }

    @Override
    public List<String> getMessageTypes() {
        String value = readHeader(NServiceBusHeaders.ENCLOSED_MESSAGE_TYPES);
        if (value == null || value.isBlank()) {
            return List.of();
        }
        return Arrays.stream(value.split(";"))
                .map(String::trim)
                .filter(type -> !type.isEmpty())
                .map(NServiceBusJsonInboundMessage::toMessageUrn)
                .distinct()
                .toList();
    }

    @Override
    public String getMessageType() {
        List<String> messageTypes = getMessageTypes();
        return messageTypes.isEmpty() ? null : messageTypes.get(0);
    }

    @Override
    public Map<String, Object> getHeaders() {
        return headers;
    }

    @Override
    public String getResponseAddress() {
        String value = readHeader(NServiceBusHeaders.REPLY_TO_ADDRESS);
        if (value == null) {
            value = readHeader("reply_to");
        }
        if (value == null || value.isBlank()) {
            return null;
        }
        return value.contains(":") ? value : "queue:" + value;
    }

    @Override
    public String getFaultAddress() {
        return null;
    }

    @Override
    public UUID getRequestId() {
        UUID relatedTo = readUuid(NServiceBusHeaders.RELATED_TO);
        if (relatedTo != null) {
            return relatedTo;
        }
        UUID messageId = readUuid(NServiceBusHeaders.MESSAGE_ID);
        return messageId != null ? messageId : readUuid("message_id");
    }

    @Override
    public UUID getCorrelationId() {
        UUID correlationId = readUuid(NServiceBusHeaders.CORRELATION_ID);
        return correlationId != null ? correlationId : readUuid("correlation_id");
    }

    @Override
    public UUID getConversationId() {
        return readUuid(NServiceBusHeaders.CONVERSATION_ID);
    }

    @SuppressWarnings("unchecked")
    @Override
    public <T> T getMessage(Type type) throws Exception {
        Object cached = messageCache.get(type);
        if (cached != null) {
            return (T) cached;
        }
        T message = (T) mapper.readValue(body, mapper.getTypeFactory().constructType(type));
        if (message != null) {
            messageCache.put(type, message);
        }
        return message;
    }

    private UUID readUuid(String name) {
        try {
            String value = readHeader(name);
            return value == null ? null : UUID.fromString(value);
        } catch (IllegalArgumentException ignored) {
            return null;
        }
    }

    private String readHeader(String name) {
        Object value = headers.get(name);
        return value instanceof byte[] bytes ? new String(bytes, StandardCharsets.UTF_8)
                : value != null ? value.toString() : null;
    }

    private static String toMessageUrn(String enclosedType) {
        String fullName = enclosedType.split(",", 2)[0].trim();
        int separator = fullName.lastIndexOf('.');
        return separator < 0
                ? "urn:message::" + fullName
                : "urn:message:" + fullName.substring(0, separator) + ":"
                        + fullName.substring(separator + 1).replace('+', '.');
    }
}
