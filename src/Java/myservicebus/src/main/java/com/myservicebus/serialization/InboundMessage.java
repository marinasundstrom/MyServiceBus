package com.myservicebus.serialization;

import java.lang.reflect.Type;
import java.util.List;
import java.util.Map;

public interface InboundMessage {
    InboundMessageFormat getFormat();

    String getContentType();

    List<String> getMessageTypes();

    String getMessageType();

    Map<String, Object> getHeaders();

    String getResponseAddress();

    String getFaultAddress();

    <T> T getMessage(Type type) throws Exception;
}
