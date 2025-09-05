package com.myservicebus;

import java.util.Map;

public class TransportMessage {
    private final byte[] body;
    private final Map<String, Object> headers;

    public TransportMessage(byte[] body, Map<String, Object> headers) {
        this.body = body;
        this.headers = headers;
    }

    public byte[] getBody() {
        return body;
    }

    public Map<String, Object> getHeaders() {
        return headers;
    }
}
