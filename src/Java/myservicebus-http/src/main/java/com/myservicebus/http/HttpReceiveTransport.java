package com.myservicebus.http;

import com.myservicebus.MessageHeaders;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.TransportMessage;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.net.InetSocketAddress;
import java.net.URI;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executors;
import java.util.function.Function;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;
import com.sun.net.httpserver.HttpServer;

public class HttpReceiveTransport implements ReceiveTransport {
    private final HttpServer server;
    private final String path;
    private final Function<TransportMessage, CompletableFuture<Void>> handler;
    private final boolean hasErrorEndpoint;
    private final Function<String, Boolean> isMessageTypeRegistered;

    public HttpReceiveTransport(HttpServer server, String path,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            boolean hasErrorEndpoint,
            Function<String, Boolean> isMessageTypeRegistered) {
        this.server = server;
        this.path = path.startsWith("/") ? path.substring(1) : path;
        this.handler = handler;
        this.hasErrorEndpoint = hasErrorEndpoint;
        this.isMessageTypeRegistered = isMessageTypeRegistered;
        this.server.createContext("/", new InternalHandler());
        this.server.setExecutor(Executors.newCachedThreadPool());
    }

    @Override
    public void start() throws Exception {
        server.start();
    }

    @Override
    public void stop() throws Exception {
        server.stop(0);
    }

    private class InternalHandler implements HttpHandler {
        @Override
        public void handle(HttpExchange exchange) throws IOException {
            try {
                if (!"POST".equalsIgnoreCase(exchange.getRequestMethod())) {
                    exchange.sendResponseHeaders(405, -1);
                    return;
                }
                String requested = exchange.getRequestURI().getPath();
                requested = requested.startsWith("/") ? requested.substring(1) : requested;
                if (!(requested.equals(path) || requested.equals(path + "_error") || requested.equals(path + "_fault"))) {
                    exchange.sendResponseHeaders(404, -1);
                    return;
                }
                byte[] payload;
                try (InputStream is = exchange.getRequestBody();
                        ByteArrayOutputStream baos = new ByteArrayOutputStream()) {
                    is.transferTo(baos);
                    payload = baos.toByteArray();
                }
                Map<String, Object> headers = new HashMap<>();
                exchange.getRequestHeaders().forEach((k, v) -> {
                    if (!v.isEmpty()) {
                        headers.put(k, v.get(0));
                    }
                });
                String host = exchange.getRequestHeaders().getFirst("Host");
                String baseUri = "http://" + host;
                boolean isFault = requested.endsWith("_fault");
                boolean isError = requested.endsWith("_error");
                if (hasErrorEndpoint && !isFault && !isError) {
                    headers.putIfAbsent(MessageHeaders.FAULT_ADDRESS, baseUri + "/" + path + "_fault");
                }
                TransportMessage tm = new TransportMessage(payload, headers);
                handler.apply(tm).join();
                exchange.sendResponseHeaders(200, -1);
            } catch (Exception ex) {
                exchange.sendResponseHeaders(500, -1);
            } finally {
                exchange.close();
            }
        }
    }

    public static HttpReceiveTransport create(URI uri,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            boolean hasErrorEndpoint,
            Function<String, Boolean> isMessageTypeRegistered) throws IOException {
        int port = uri.getPort() == -1 ? 80 : uri.getPort();
        HttpServer server = HttpServer.create(new InetSocketAddress(uri.getHost(), port), 0);
        return new HttpReceiveTransport(server, uri.getPath(), handler, hasErrorEndpoint, isMessageTypeRegistered);
    }
}
