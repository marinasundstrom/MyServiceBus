package com.myservicebus.serialization;

import com.myservicebus.HostInfo;
import java.net.URI;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;

public class MessageSerializationContext<T> {
    private UUID messageId;
    private UUID correlationId;
    private List<String> messageType;
    private URI responseAddress;
    private URI faultAddress;
    private Map<String, Object> headers;
    private OffsetDateTime sentTime;
    private T message;
    private HostInfo hostInfo;

    public MessageSerializationContext(T message) {
        this.message = message;
    }

    public UUID getMessageId() {
        return messageId;
    }

    public void setMessageId(UUID messageId) {
        this.messageId = messageId;
    }

    public UUID getCorrelationId() {
        return correlationId;
    }

    public void setCorrelationId(UUID correlationId) {
        this.correlationId = correlationId;
    }

    public List<String> getMessageType() {
        return messageType;
    }

    public void setMessageType(List<String> messageType) {
        this.messageType = messageType;
    }

    public URI getResponseAddress() {
        return responseAddress;
    }

    public void setResponseAddress(URI responseAddress) {
        this.responseAddress = responseAddress;
    }

    public URI getFaultAddress() {
        return faultAddress;
    }

    public void setFaultAddress(URI faultAddress) {
        this.faultAddress = faultAddress;
    }

    public Map<String, Object> getHeaders() {
        return headers;
    }

    public void setHeaders(Map<String, Object> headers) {
        this.headers = headers;
    }

    public OffsetDateTime getSentTime() {
        return sentTime;
    }

    public void setSentTime(OffsetDateTime sentTime) {
        this.sentTime = sentTime;
    }

    public T getMessage() {
        return message;
    }

    public void setMessage(T message) {
        this.message = message;
    }

    public HostInfo getHostInfo() {
        return hostInfo;
    }

    public void setHostInfo(HostInfo hostInfo) {
        this.hostInfo = hostInfo;
    }
}

