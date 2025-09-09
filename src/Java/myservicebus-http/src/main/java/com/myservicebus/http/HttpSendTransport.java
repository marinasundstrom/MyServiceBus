package com.myservicebus.http;

import com.myservicebus.SendTransport;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.util.Map;

public class HttpSendTransport implements SendTransport {
    private final HttpClient client;
    private final URI address;

    public HttpSendTransport(HttpClient client, URI address) {
        this.client = client;
        this.address = address;
    }

    @Override
    public void send(byte[] data, Map<String, Object> headers, String contentType) {
        try {
            HttpRequest.Builder builder = HttpRequest.newBuilder(address)
                    .POST(HttpRequest.BodyPublishers.ofByteArray(data));
            if (contentType != null) {
                builder.header("Content-Type", contentType);
            }
            if (headers != null) {
                for (Map.Entry<String, Object> entry : headers.entrySet()) {
                    if (entry.getValue() != null) {
                        builder.header(entry.getKey(), entry.getValue().toString());
                    }
                }
            }
            client.send(builder.build(), HttpResponse.BodyHandlers.discarding());
        } catch (Exception ex) {
            throw new RuntimeException("Failed to send HTTP request", ex);
        }
    }
}
