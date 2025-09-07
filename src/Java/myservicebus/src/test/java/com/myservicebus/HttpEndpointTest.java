package com.myservicebus;

import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.URI;
import java.net.http.HttpClient;
import java.util.concurrent.Executors;

import static org.junit.jupiter.api.Assertions.*;

public class HttpEndpointTest {
    private HttpServer server;
    private HttpExchange lastExchange;

    @BeforeEach
    public void startServer() throws IOException {
        server = HttpServer.create(new InetSocketAddress(0), 0);
        server.createContext("/hook", exchange -> {
            lastExchange = exchange;
            exchange.sendResponseHeaders(200, 0);
            try (OutputStream os = exchange.getResponseBody()) {
            }
        });
        server.setExecutor(Executors.newSingleThreadExecutor());
        server.start();
    }

    @AfterEach
    public void stopServer() {
        server.stop(0);
    }

    @Test
    public void send_posts_message() throws Exception {
        var client = HttpClient.newHttpClient();
        var endpoint = new HttpEndpoint(client, URI.create("http://localhost:" + server.getAddress().getPort() + "/hook"));
        endpoint.send("hi").join();
        assertNotNull(lastExchange);
        assertEquals("POST", lastExchange.getRequestMethod());
    }
}
