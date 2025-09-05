package com.myservicebus;

import java.util.Map;

public interface SendTransport {
    default void send(byte[] data) {
        send(data, Map.of(), "application/vnd.masstransit+json");
    }

    void send(byte[] data, Map<String, Object> headers, String contentType);
}
