package com.myservicebus.serialization;

import java.lang.reflect.Type;
import java.util.List;
import java.util.Map;
import java.util.HashMap;
import java.util.concurrent.ConcurrentHashMap;
import java.util.UUID;

import com.myservicebus.Envelope;
import com.fasterxml.jackson.databind.JavaType;
import com.fasterxml.jackson.databind.ObjectMapper;

public class EnvelopeInboundMessage implements InboundMessage {
    private final byte[] body;
    private final Map<String, Object> transportHeaders;
    private final ObjectMapper mapper;
    private final MessageHeaderConvention headerConvention;
    private final Envelope<Object> metadataEnvelope;
    private final Map<Type, Object> messageCache = new ConcurrentHashMap<>();
    private Map<String, Object> headers;

    public EnvelopeInboundMessage(byte[] body, Map<String, Object> transportHeaders, ObjectMapper mapper, MessageHeaderConvention headerConvention) throws java.io.IOException {
        this.body = body;
        this.transportHeaders = transportHeaders;
        this.mapper = mapper;
        this.headerConvention = headerConvention;
        this.metadataEnvelope = deserializeEnvelope(Object.class);
    }

    @Override
    public InboundMessageFormat getFormat() {
        return InboundMessageFormat.ENVELOPE;
    }

    @Override
    public String getContentType() {
        return DefaultInboundMessageResolver.ENVELOPE_CONTENT_TYPE;
    }

    @Override
    public List<String> getMessageTypes() {
        return metadataEnvelope.getMessageType() != null ? metadataEnvelope.getMessageType() : List.of();
    }

    @Override
    public String getMessageType() {
        return getMessageTypes().isEmpty() ? null : getMessageTypes().get(0);
    }

    @Override
    public Map<String, Object> getHeaders() {
        if (headers != null) {
            return headers;
        }

        headers = metadataEnvelope.getHeaders() != null
                ? new HashMap<>(metadataEnvelope.getHeaders())
                : new HashMap<>();
        headers.putAll(transportHeaders);
        return headers;
    }

    @Override
    public String getResponseAddress() {
        return metadataEnvelope.getResponseAddress();
    }

    @Override
    public String getFaultAddress() {
        String faultAddress = metadataEnvelope.getFaultAddress();
        if (faultAddress != null) {
            return faultAddress;
        }

        Object value = transportHeaders.get(headerConvention.getFaultAddressHeader());
        if (value instanceof byte[] bytes) {
            return new String(bytes, java.nio.charset.StandardCharsets.UTF_8);
        }

        return value != null ? value.toString() : null;
    }

    @Override
    public UUID getRequestId() {
        return metadataEnvelope.getRequestId();
    }

    @Override
    public UUID getCorrelationId() {
        return metadataEnvelope.getCorrelationId();
    }

    @Override
    public UUID getConversationId() {
        return metadataEnvelope.getConversationId();
    }

    @Override
    public UUID getInitiatorId() {
        return metadataEnvelope.getInitiatorId();
    }

    @SuppressWarnings("unchecked")
    @Override
    public <T> T getMessage(Type type) throws Exception {
        Object cached = messageCache.get(type);
        if (cached != null) {
            return (T) cached;
        }

        Envelope<T> typedEnvelope = deserializeEnvelope(type);
        T message = typedEnvelope.getMessage();
        if (message != null) {
            messageCache.put(type, message);
        }
        return message;
    }

    private <T> Envelope<T> deserializeEnvelope(Type type) throws java.io.IOException {
        JavaType messageType = mapper.getTypeFactory().constructType(type);
        JavaType envelopeType = mapper.getTypeFactory().constructParametricType(Envelope.class, messageType);
        return mapper.readValue(body, envelopeType);
    }
}
