package com.myservicebus;

import com.fasterxml.jackson.databind.ObjectMapper;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.util.EnumSet;
import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;

public class HttpEndpoint implements Endpoint {
    private final HttpClient client;
    private final URI uri;
    private final ObjectMapper mapper;

    public HttpEndpoint(HttpClient client, URI uri) {
        this.client = client;
        this.uri = uri;
        this.mapper = new ObjectMapper();
        this.mapper.findAndRegisterModules();
    }

    @Override
    public <T> CompletableFuture<Void> send(T message, Consumer<SendContext> configure) throws Exception {
        String json = mapper.writeValueAsString(message);
        HttpRequest request = HttpRequest.newBuilder(uri)
                .header("Content-Type", "application/json")
                .POST(HttpRequest.BodyPublishers.ofString(json))
                .build();
        return client.sendAsync(request, HttpResponse.BodyHandlers.discarding()).thenApply(r -> null);
    }

    @Override
    public EnumSet<EndpointCapability> getCapabilities() {
        return EnumSet.noneOf(EndpointCapability.class);
    }
}
