package com.myservicebus.serialization;

import java.lang.reflect.Type;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

import com.fasterxml.jackson.databind.ObjectMapper;

public class RawJsonInboundMessage implements InboundMessage {
    private final byte[] body;
    private final Map<String, Object> headers;
    private final ObjectMapper mapper;
    private final MessageHeaderConvention headerConvention;
    private final Map<Type, Object> messageCache = new ConcurrentHashMap<>();

    public RawJsonInboundMessage(byte[] body, Map<String, Object> headers, ObjectMapper mapper, MessageHeaderConvention headerConvention) {
        this.body = body;
        this.headers = headers;
        this.mapper = mapper;
        this.headerConvention = headerConvention;
    }

    @Override
    public InboundMessageFormat getFormat() {
        return InboundMessageFormat.RAW_JSON;
    }

    @Override
    public String getContentType() {
        return DefaultInboundMessageResolver.RAW_JSON_CONTENT_TYPE;
    }

    @Override
    public List<String> getMessageTypes() {
        return List.of();
    }

    @Override
    public String getMessageType() {
        return null;
    }

    @Override
    public Map<String, Object> getHeaders() {
        return headers;
    }

    @Override
    public String getResponseAddress() {
        return null;
    }

    @Override
    public String getFaultAddress() {
        Object value = headers.get(headerConvention.getFaultAddressHeader());
        if (value instanceof byte[] bytes) {
            return new String(bytes, StandardCharsets.UTF_8);
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

        T message = (T) mapper.readValue(body, mapper.getTypeFactory().constructType(type));
        if (message != null) {
            messageCache.put(type, message);
        }
        return message;
    }
}
