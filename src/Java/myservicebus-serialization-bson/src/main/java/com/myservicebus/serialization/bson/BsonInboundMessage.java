package com.myservicebus.serialization.bson;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.JavaType;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;
import com.myservicebus.serialization.InboundMessage;
import com.myservicebus.serialization.InboundMessageFormat;
import com.myservicebus.serialization.MessageHeaderConvention;
import java.lang.reflect.Type;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

final class BsonInboundMessage implements InboundMessage {
    private static final TypeReference<Map<String, Object>> HEADER_MAP = new TypeReference<>() {
    };

    private final ObjectNode envelope;
    private final Map<String, Object> transportHeaders;
    private final ObjectMapper applicationMapper;
    private final MessageHeaderConvention headerConvention;
    private final Map<Type, Object> messageCache = new ConcurrentHashMap<>();
    private Map<String, Object> headers;

    BsonInboundMessage(
            ObjectNode envelope,
            Map<String, Object> transportHeaders,
            ObjectMapper applicationMapper,
            MessageHeaderConvention headerConvention) {
        this.envelope = envelope;
        this.transportHeaders = transportHeaders;
        this.applicationMapper = applicationMapper;
        this.headerConvention = headerConvention;
    }

    @Override
    public InboundMessageFormat getFormat() {
        return InboundMessageFormat.ENVELOPE;
    }

    @Override
    public String getContentType() {
        return BsonSerializerFactory.BSON_CONTENT_TYPE;
    }

    @Override
    public List<String> getMessageTypes() {
        JsonNode values = envelope.get("messageType");
        if (values == null || !values.isArray()) {
            return List.of();
        }
        List<String> result = new ArrayList<>();
        values.forEach(value -> result.add(value.asText()));
        return result;
    }

    @Override
    public String getMessageType() {
        List<String> values = getMessageTypes();
        return values.isEmpty() ? null : values.get(0);
    }

    @Override
    public Map<String, Object> getHeaders() {
        if (headers != null) {
            return headers;
        }
        JsonNode envelopeHeaders = envelope.get("headers");
        headers = envelopeHeaders != null && envelopeHeaders.isObject()
                ? new HashMap<>(applicationMapper.convertValue(envelopeHeaders, HEADER_MAP))
                : new HashMap<>();
        headers.putAll(transportHeaders);
        return headers;
    }

    @Override
    public String getResponseAddress() {
        return text("responseAddress");
    }

    @Override
    public String getFaultAddress() {
        String envelopeAddress = text("faultAddress");
        if (envelopeAddress != null) {
            return envelopeAddress;
        }
        Object value = transportHeaders.get(headerConvention.getFaultAddressHeader());
        if (value instanceof byte[] bytes) {
            return new String(bytes, StandardCharsets.UTF_8);
        }
        return value != null ? value.toString() : null;
    }

    @Override
    public UUID getRequestId() {
        return uuid("requestId");
    }

    @Override
    public UUID getCorrelationId() {
        return uuid("correlationId");
    }

    @Override
    public UUID getConversationId() {
        return uuid("conversationId");
    }

    @Override
    public UUID getInitiatorId() {
        return uuid("initiatorId");
    }

    @SuppressWarnings("unchecked")
    @Override
    public <T> T getMessage(Type type) throws Exception {
        Object cached = messageCache.get(type);
        if (cached != null) {
            return (T) cached;
        }
        JsonNode messageNode = envelope.get("message");
        if (messageNode == null || messageNode.isNull()) {
            return null;
        }
        JavaType javaType = applicationMapper.getTypeFactory().constructType(type);
        T message = applicationMapper.readerFor(javaType).readValue(messageNode);
        if (message != null) {
            messageCache.put(type, message);
        }
        return message;
    }

    private UUID uuid(String name) {
        String value = text(name);
        try {
            return value != null ? UUID.fromString(value) : null;
        } catch (IllegalArgumentException ignored) {
            return null;
        }
    }

    private String text(String name) {
        JsonNode value = envelope.get(name);
        return value != null && value.isTextual() ? value.asText() : null;
    }
}
