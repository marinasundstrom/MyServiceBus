package com.myservicebus;

import java.net.URI;
import java.util.List;
import java.util.Map;

public class ReceiveContext<T> implements PipeContext {
    private final T message;
    private final List<String> messageType;
    private final Map<String, Object> headers;
    private final URI responseAddress;
    private final URI faultAddress;
    private final URI errorAddress;

    public ReceiveContext(T message, List<String> messageType, Map<String, Object> headers,
                          URI responseAddress, URI faultAddress, URI errorAddress) {
        this.message = message;
        this.messageType = messageType;
        this.headers = headers;
        this.responseAddress = responseAddress;
        this.faultAddress = faultAddress;
        this.errorAddress = errorAddress;
    }

    public T getMessage() { return message; }
    public List<String> getMessageType() { return messageType; }
    public Map<String, Object> getHeaders() { return headers; }
    public URI getResponseAddress() { return responseAddress; }
    public URI getFaultAddress() { return faultAddress; }
    public URI getErrorAddress() { return errorAddress; }
}
