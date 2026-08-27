package com.myservicebus.monitoring;

import static org.junit.jupiter.api.Assertions.assertTrue;

import java.net.InetSocketAddress;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

import org.junit.jupiter.api.Test;

import com.myservicebus.MessageOperationHookEvent;
import com.myservicebus.inspection.BusInspectionSnapshot;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;

class MonitoringExporterTest {
    @Test
    void exporterRegistersMetadataAndSendsObservationBatches() throws Exception {
        CountDownLatch metadataReceived = new CountDownLatch(1);
        CountDownLatch batchReceived = new CountDownLatch(1);
        AtomicReference<String> metadataJson = new AtomicReference<>();
        AtomicReference<String> batchJson = new AtomicReference<>();
        HttpServer server = HttpServer.create(new InetSocketAddress("localhost", 0), 0);
        server.createContext("/api/monitoring/v1/metadata", exchange -> {
            metadataJson.set(readBody(exchange));
            respond(exchange);
            metadataReceived.countDown();
        });
        server.createContext("/api/monitoring/v1/observations:batch", exchange -> {
            batchJson.set(readBody(exchange));
            respond(exchange);
            batchReceived.countDown();
        });
        server.createContext("/api/monitoring/v1/heartbeat", exchange -> {
            readBody(exchange);
            respond(exchange);
        });
        server.start();

        MonitoringExporterOptions options = new MonitoringExporterOptions();
        options.setServiceAddress(URI.create("http://localhost:" + server.getAddress().getPort()));
        options.setApplicationName("orders-java");
        options.getLabels().put("group", "commerce");
        options.setExportInterval(Duration.ofMillis(20));
        options.setHeartbeatInterval(Duration.ofSeconds(1));
        options.setMaxBatchSize(1);

        MonitoringExporter exporter = new MonitoringExporter(options);
        try {
            exporter.start(() -> new BusInspectionSnapshot(
                    "mediator",
                    URI.create("loopback://localhost/"),
                    Instant.now(),
                    List.of(),
                    List.of(),
                    List.of()));
            exporter.handle(MessageOperationHookEvent.create(
                    "published",
                    true,
                    TestMessage.class,
                    null,
                    "loopback://test-message",
                    System.nanoTime(),
                    null,
                    null,
                    null));

            assertTrue(metadataReceived.await(2, TimeUnit.SECONDS));
            assertTrue(batchReceived.await(2, TimeUnit.SECONDS));
            assertTrue(metadataJson.get().contains("\"startedAtUtc\":\""));
            assertTrue(metadataJson.get().contains("\"labels\":{\"group\":\"commerce\"}"));
            assertTrue(batchJson.get().contains("\"applicationName\":\"orders-java\""));
            assertTrue(batchJson.get().contains("\"exportedAtUtc\":\""));
            assertTrue(batchJson.get().contains("\"kind\":\"published\""));
        } finally {
            exporter.close();
            server.stop(0);
        }
    }

    private static String readBody(HttpExchange exchange) throws java.io.IOException {
        return new String(exchange.getRequestBody().readAllBytes(), StandardCharsets.UTF_8);
    }

    private static void respond(HttpExchange exchange) throws java.io.IOException {
        exchange.sendResponseHeaders(202, -1);
        exchange.close();
    }

    private record TestMessage(String value) {
    }
}
