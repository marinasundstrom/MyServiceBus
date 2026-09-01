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
import com.myservicebus.JobAttemptState;
import com.myservicebus.JobAttemptStatus;
import com.myservicebus.JobProgress;
import com.myservicebus.JobSource;
import com.myservicebus.JobState;
import com.myservicebus.JobStatus;
import com.myservicebus.OutboxDeliveryHookEvent;
import com.myservicebus.SchedulingDurability;
import com.myservicebus.SchedulingPlacement;
import com.myservicebus.ScheduledWorkSource;
import com.myservicebus.ScheduledWorkState;
import com.myservicebus.ScheduledWorkStatus;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.inspection.BusInspectionProvider;
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
        server.createContext("/api/monitoring/v1/scheduled-work", exchange -> {
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
                    null,
                    null,
                    null,
                    "message-1",
                    "trigger-1"));

            assertTrue(metadataReceived.await(2, TimeUnit.SECONDS));
            assertTrue(batchReceived.await(2, TimeUnit.SECONDS));
            assertTrue(metadataJson.get().contains("\"startedAtUtc\":\""));
            assertTrue(metadataJson.get().contains("\"labels\":{\"group\":\"commerce\"}"));
            assertTrue(batchJson.get().contains("\"applicationName\":\"orders-java\""));
            assertTrue(batchJson.get().contains("\"exportedAtUtc\":\""));
            assertTrue(batchJson.get().contains("\"kind\":\"published\""));
            assertTrue(batchJson.get().contains("\"messageId\":\"message-1\""));
            assertTrue(batchJson.get().contains("\"causationMessageId\":\"trigger-1\""));
        } finally {
            exporter.close();
            server.stop(0);
        }
    }

    @Test
    void exporterMapsBoundedOutboxDispatchProperties() throws Exception {
        CountDownLatch batchReceived = new CountDownLatch(1);
        AtomicReference<String> batchJson = new AtomicReference<>();
        HttpServer server = HttpServer.create(new InetSocketAddress("localhost", 0), 0);
        server.createContext("/api/monitoring/v1/metadata", exchange -> {
            readBody(exchange);
            respond(exchange);
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
        server.createContext("/api/monitoring/v1/scheduled-work", exchange -> {
            readBody(exchange);
            respond(exchange);
        });
        server.start();

        MonitoringExporterOptions options = new MonitoringExporterOptions();
        options.setServiceAddress(URI.create("http://localhost:" + server.getAddress().getPort()));
        options.setApplicationName("dispatcher-java");
        options.setExportInterval(Duration.ofMillis(20));
        options.setHeartbeatInterval(Duration.ofSeconds(1));
        options.setMaxBatchSize(1);

        MonitoringExporter exporter = new MonitoringExporter(options);
        try {
            exporter.start(() -> new BusInspectionSnapshot(
                    "mediator", URI.create("loopback://localhost/"), Instant.now(),
                    List.of(), List.of(), List.of()));
            exporter.handle(new OutboxDeliveryHookEvent(
                    Instant.now(), "orders-service", "orders-dispatcher-a", true, 12.5,
                    8, 7, 1, 0, 11, 2, 3, 40, 1, 4, 2_500.0, null));

            assertTrue(batchReceived.await(2, TimeUnit.SECONDS));
            assertTrue(batchJson.get().contains("\"kind\":\"outbox_dispatch_cycle\""));
            assertTrue(batchJson.get().contains("\"service_name\":\"orders-service\""));
            assertTrue(batchJson.get().contains("\"batch_dispatched\":\"7\""));
            assertTrue(batchJson.get().contains("\"pending\":\"11\""));
            assertTrue(!batchJson.get().contains("message_id"));
        } finally {
            exporter.close();
            server.stop(0);
        }
    }

    @Test
    void exporterSendsScheduledWorkSnapshotsWithoutMessageBodies() throws Exception {
        CountDownLatch scheduledReceived = new CountDownLatch(1);
        AtomicReference<String> scheduledJson = new AtomicReference<>();
        HttpServer server = HttpServer.create(new InetSocketAddress("localhost", 0), 0);
        server.createContext("/api/monitoring/v1/metadata", exchange -> {
            readBody(exchange);
            respond(exchange);
        });
        server.createContext("/api/monitoring/v1/scheduled-work", exchange -> {
            scheduledJson.set(readBody(exchange));
            respond(exchange);
            scheduledReceived.countDown();
        });
        server.start();

        MonitoringExporterOptions options = new MonitoringExporterOptions();
        options.setServiceAddress(URI.create("http://localhost:" + server.getAddress().getPort()));
        options.setExportInterval(Duration.ofMillis(20));
        MonitoringExporter exporter = new MonitoringExporter(options);
        try {
            exporter.observe(new ScheduledWorkState(
                    java.util.UUID.randomUUID(), "InMemory", SchedulingDurability.VOLATILE,
                    "Message", TestMessage.class.getName(), "Publish", null, Instant.now().plusSeconds(60),
                    ScheduledWorkStatus.PENDING, "Pending", 0, Instant.now(), null));
            exporter.start(() -> new BusInspectionSnapshot(
                    "mediator", URI.create("loopback://localhost/"), Instant.now(),
                    List.of(), List.of(), List.of()));

            assertTrue(scheduledReceived.await(2, TimeUnit.SECONDS));
            assertTrue(scheduledJson.get().contains("\"status\":\"Pending\""));
            assertTrue(scheduledJson.get().contains("\"messageType\":"));
            assertTrue(!scheduledJson.get().contains("secret-body"));
        } finally {
            exporter.close();
            server.stop(0);
        }
    }

    @Test
    void exporterRestoresScheduledWorkFromAuthoritativeSource() throws Exception {
        CountDownLatch scheduledReceived = new CountDownLatch(1);
        AtomicReference<String> scheduledJson = new AtomicReference<>();
        HttpServer server = HttpServer.create(new InetSocketAddress("localhost", 0), 0);
        server.createContext("/api/monitoring/v1/metadata", exchange -> {
            readBody(exchange);
            respond(exchange);
        });
        server.createContext("/api/monitoring/v1/scheduled-work", exchange -> {
            scheduledJson.set(readBody(exchange));
            respond(exchange);
            scheduledReceived.countDown();
        });
        server.start();

        ScheduledWorkState state = new ScheduledWorkState(
                java.util.UUID.randomUUID(), "PostgreSQL", SchedulingDurability.DURABLE,
                "Message", TestMessage.class.getName(), "Publish", null, Instant.now().plusSeconds(60),
                ScheduledWorkStatus.PENDING, "Pending", 0, Instant.now(), null);
        ScheduledWorkSource source = new ScheduledWorkSource() {
            @Override
            public String getProvider() {
                return "PostgreSQL";
            }

            @Override
            public boolean isAuthoritative() {
                return true;
            }

            @Override
            public java.util.concurrent.CompletionStage<List<ScheduledWorkState>> getSnapshot(int maximumCount) {
                return java.util.concurrent.CompletableFuture.completedFuture(List.of(state));
            }
        };
        MonitoringExporterOptions options = new MonitoringExporterOptions();
        options.setServiceAddress(URI.create("http://localhost:" + server.getAddress().getPort()));
        options.setExportInterval(Duration.ofMillis(20));
        MonitoringExporter exporter = new MonitoringExporter(options);
        try {
            ServiceCollection services = ServiceCollection.create();
            services.addSingleton(BusInspectionProvider.class, ignored -> () -> () -> new BusInspectionSnapshot(
                    "mediator", URI.create("loopback://localhost/"), Instant.now(),
                    List.of(), List.of(), List.of()));
            services.addSingleton(ScheduledWorkSource.class, ignored -> () -> source);
            ServiceProvider provider = services.buildServiceProvider();
            exporter.start(provider);

            assertTrue(scheduledReceived.await(2, TimeUnit.SECONDS));
            assertTrue(scheduledJson.get().contains(state.tokenId().toString()));
            assertTrue(scheduledJson.get().contains("\"provider\":\"PostgreSQL\""));
            assertTrue(scheduledJson.get().contains("\"durability\":\"Durable\""));
        } finally {
            exporter.close();
            server.stop(0);
        }
    }

    @Test
    void exporterSendsJobsWithBoundedAttemptsWithoutPayloads() throws Exception {
        CountDownLatch jobsReceived = new CountDownLatch(1);
        AtomicReference<String> jobsJson = new AtomicReference<>();
        HttpServer server = HttpServer.create(new InetSocketAddress("localhost", 0), 0);
        server.createContext("/api/monitoring/v1/metadata", exchange -> {
            readBody(exchange);
            respond(exchange);
        });
        server.createContext("/api/monitoring/v1/scheduled-work", exchange -> {
            readBody(exchange);
            respond(exchange);
        });
        server.createContext("/api/monitoring/v1/recurring-jobs", exchange -> {
            readBody(exchange);
            respond(exchange);
        });
        server.createContext("/api/monitoring/v1/jobs", exchange -> {
            jobsJson.set(readBody(exchange));
            respond(exchange);
            jobsReceived.countDown();
        });
        server.start();

        Instant now = Instant.now();
        java.util.UUID occurrenceId = java.util.UUID.randomUUID();
        JobState state = new JobState(
                java.util.UUID.randomUUID(), "invoice-export", JobStatus.FAULTED, "in-memory",
                SchedulingDurability.VOLATILE, SchedulingPlacement.PROCESS_LOCAL,
                now.minusSeconds(2), null, now.minusSeconds(1), now,
                new JobProgress(4, 10L), occurrenceId, now);
        JobAttemptState attempt = new JobAttemptState(
                java.util.UUID.randomUUID(), state.jobId(), 0, JobAttemptStatus.FAULTED,
                now.minusSeconds(1), now, "java.lang.IllegalStateException", "secret-body");
        JobSource source = new JobSource() {
            @Override
            public String getProvider() {
                return "in-memory";
            }

            @Override
            public boolean isAuthoritative() {
                return true;
            }

            @Override
            public java.util.concurrent.CompletionStage<List<JobState>> getSnapshot(
                    int maximumCount,
                    com.myservicebus.tasks.CancellationToken cancellationToken) {
                return java.util.concurrent.CompletableFuture.completedFuture(List.of(state));
            }

            @Override
            public java.util.concurrent.CompletionStage<List<JobAttemptState>> getAttempts(
                    java.util.UUID jobId,
                    int maximumCount,
                    com.myservicebus.tasks.CancellationToken cancellationToken) {
                return java.util.concurrent.CompletableFuture.completedFuture(List.of(attempt));
            }
        };
        MonitoringExporterOptions options = new MonitoringExporterOptions();
        options.setServiceAddress(URI.create("http://localhost:" + server.getAddress().getPort()));
        options.setExportInterval(Duration.ofMillis(20));
        MonitoringExporter exporter = new MonitoringExporter(options);
        try {
            ServiceCollection services = ServiceCollection.create();
            services.addSingleton(BusInspectionProvider.class, ignored -> () -> () -> new BusInspectionSnapshot(
                    "mediator", URI.create("loopback://localhost/"), Instant.now(),
                    List.of(), List.of(), List.of()));
            services.addSingleton(JobSource.class, ignored -> () -> source);
            exporter.start(services.buildServiceProvider());

            assertTrue(jobsReceived.await(2, TimeUnit.SECONDS));
            assertTrue(jobsJson.get().contains(state.jobId().toString()));
            assertTrue(jobsJson.get().contains(occurrenceId.toString()));
            assertTrue(jobsJson.get().contains("java.lang.IllegalStateException"));
            assertTrue(!jobsJson.get().contains("secret-body"));
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
