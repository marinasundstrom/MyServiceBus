package com.myservicebus.serialization;

import java.lang.reflect.Type;
import java.util.List;
import java.util.Map;
import java.util.UUID;

public interface InboundMessage {
    InboundMessageFormat getFormat();

    String getContentType();

    List<String> getMessageTypes();

    String getMessageType();

    Map<String, Object> getHeaders();

    String getResponseAddress();

    String getFaultAddress();

    default UUID getRequestId() {
        return null;
    }

    default UUID getCorrelationId() {
        return null;
    }

    <T> T getMessage(Type type) throws Exception;
}
