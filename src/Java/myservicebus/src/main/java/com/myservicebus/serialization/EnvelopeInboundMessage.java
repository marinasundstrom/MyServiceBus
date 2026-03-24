package com.myservicebus.serialization;

import java.lang.reflect.Type;
import java.util.List;
import java.util.Map;
import java.util.HashMap;
import java.util.concurrent.ConcurrentHashMap;

import com.myservicebus.Envelope;

public class EnvelopeInboundMessage implements InboundMessage {
    private final byte[] body;
    private final Map<String, Object> transportHeaders;
    private final MessageDeserializer deserializer;
    private final MessageHeaderConvention headerConvention;
    private final Envelope<Object> metadataEnvelope;
    private final Map<Type, Object> messageCache = new ConcurrentHashMap<>();
    private Map<String, Object> headers;

    public EnvelopeInboundMessage(byte[] body, Map<String, Object> transportHeaders, MessageDeserializer deserializer, MessageHeaderConvention headerConvention) throws Exception {
        this.body = body;
        this.transportHeaders = transportHeaders;
        this.deserializer = deserializer;
        this.headerConvention = headerConvention;
        this.metadataEnvelope = deserializer.deserialize(body, Object.class);
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

    @SuppressWarnings("unchecked")
    @Override
    public <T> T getMessage(Type type) throws Exception {
        Object cached = messageCache.get(type);
        if (cached != null) {
            return (T) cached;
        }

        Envelope<T> typedEnvelope = deserializer.deserialize(body, type);
        T message = typedEnvelope.getMessage();
        if (message != null) {
            messageCache.put(type, message);
        }
        return message;
    }
}
