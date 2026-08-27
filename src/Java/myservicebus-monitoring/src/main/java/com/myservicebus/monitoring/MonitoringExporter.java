package com.myservicebus.monitoring;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicLong;
import java.util.concurrent.atomic.AtomicBoolean;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.databind.json.JsonMapper;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import com.myservicebus.BusHook;
import com.myservicebus.BusHookEvent;
import com.myservicebus.BusLifecycleHookEvent;
import com.myservicebus.MessageOperationHookEvent;
import com.myservicebus.inspection.BusInspectionProvider;

public final class MonitoringExporter implements BusHook, AutoCloseable {
    private final MonitoringExporterOptions options;
    private final HttpClient httpClient = HttpClient.newHttpClient();
    private final ObjectMapper objectMapper = JsonMapper.builder()
            .addModule(new JavaTimeModule())
            .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS)
            .build();
    private final ArrayBlockingQueue<MonitoringProtocol.Observation> observations;
    private final ScheduledExecutorService worker = Executors.newSingleThreadScheduledExecutor(runnable -> {
        Thread thread = new Thread(runnable, "myservicebus-monitoring-exporter");
        thread.setDaemon(true);
        return thread;
    });
    private final AtomicLong sequence = new AtomicLong();
    private final AtomicLong dropped = new AtomicLong();
    private final AtomicBoolean started = new AtomicBoolean();
    private final AtomicBoolean closed = new AtomicBoolean();
    private final Instant startedAtUtc = Instant.now();
    private volatile BusInspectionProvider inspectionProvider;
    private volatile boolean metadataRegistered;
    private List<MonitoringProtocol.Observation> pendingBatch;

    public MonitoringExporter(MonitoringExporterOptions options) {
        this.options = options;
        if (options.getExportInterval().isZero() || options.getExportInterval().isNegative()) {
            throw new IllegalArgumentException("Export interval must be greater than zero");
        }
        if (options.getHeartbeatInterval().isZero() || options.getHeartbeatInterval().isNegative()) {
            throw new IllegalArgumentException("Heartbeat interval must be greater than zero");
        }
        if (options.getMaxBatchSize() <= 0 || options.getMaxQueueSize() <= 0) {
            throw new IllegalArgumentException("Batch and queue sizes must be greater than zero");
        }
        this.observations = new ArrayBlockingQueue<>(options.getMaxQueueSize());
    }

    public void start(BusInspectionProvider inspectionProvider) {
        this.inspectionProvider = inspectionProvider;
        if (!started.compareAndSet(false, true)) {
            return;
        }
        worker.scheduleWithFixedDelay(this::exportSafely, 0,
                options.getExportInterval().toMillis(), TimeUnit.MILLISECONDS);
        worker.scheduleWithFixedDelay(this::heartbeatSafely,
                options.getHeartbeatInterval().toMillis(),
                options.getHeartbeatInterval().toMillis(),
                TimeUnit.MILLISECONDS);
    }

    @Override
    public void handle(BusHookEvent busEvent) {
        MonitoringProtocol.Observation observation = map(busEvent);
        if (observation != null && !observations.offer(observation)) {
            dropped.incrementAndGet();
        } else if (observation != null && started.get() && !closed.get()
                && observations.size() >= options.getMaxBatchSize()) {
            worker.execute(this::exportSafely);
        }
    }

    private void exportSafely() {
        try {
            ensureMetadata();
            if (pendingBatch == null) {
                pendingBatch = new ArrayList<>(options.getMaxBatchSize());
                observations.drainTo(pendingBatch, options.getMaxBatchSize());
            }
            if (pendingBatch.isEmpty()) {
                pendingBatch = null;
                return;
            }

            long droppedCount = dropped.getAndSet(0);
            MonitoringProtocol.ObservationBatch batch = new MonitoringProtocol.ObservationBatch(
                    MonitoringProtocol.VERSION,
                    options.getApplicationName(),
                    options.getInstanceId(),
                    options.getBusId(),
                    UUID.randomUUID().toString().replace("-", ""),
                    pendingBatch.get(0).sequence(),
                    pendingBatch.get(pendingBatch.size() - 1).sequence(),
                    droppedCount,
                    Instant.now(),
                    List.copyOf(pendingBatch));
            try {
                if (post("/api/monitoring/v1/observations:batch", batch)) {
                    pendingBatch = null;
                } else {
                    dropped.addAndGet(droppedCount);
                }
            } catch (Exception exception) {
                dropped.addAndGet(droppedCount);
                throw exception;
            }
        } catch (Exception exception) {
            // Monitoring is best effort and must not affect messaging.
        }
    }

    private void heartbeatSafely() {
        try {
            ensureMetadata();
            post("/api/monitoring/v1/heartbeat", new MonitoringProtocol.Heartbeat(
                    MonitoringProtocol.VERSION,
                    options.getApplicationName(),
                    options.getInstanceId(),
                    options.getBusId(),
                    Instant.now()));
        } catch (Exception exception) {
            // The next scheduled export or heartbeat retries registration.
        }
    }

    private void ensureMetadata() throws Exception {
        if (metadataRegistered) {
            return;
        }
        BusInspectionProvider provider = inspectionProvider;
        if (provider == null) {
            return;
        }
        MonitoringProtocol.Metadata metadata = new MonitoringProtocol.Metadata(
                MonitoringProtocol.VERSION,
                options.getApplicationName(),
                options.getInstanceId(),
                options.getApplicationVersion(),
                "java",
                MonitoringExporter.class.getPackage().getImplementationVersion() == null
                        ? "unknown"
                        : MonitoringExporter.class.getPackage().getImplementationVersion(),
                options.getBusId(),
                startedAtUtc,
                Instant.now(),
                provider.getSnapshot());
        metadataRegistered = post("/api/monitoring/v1/metadata", metadata);
    }

    private boolean post(String path, Object body) throws Exception {
        String json = objectMapper.writeValueAsString(body);
        URI uri = options.getServiceAddress().resolve(path);
        HttpRequest request = HttpRequest.newBuilder(uri)
                .header("Content-Type", "application/json")
                .POST(HttpRequest.BodyPublishers.ofString(json))
                .build();
        HttpResponse<Void> response = httpClient.send(request, HttpResponse.BodyHandlers.discarding());
        return response.statusCode() >= 200 && response.statusCode() < 300;
    }

    private MonitoringProtocol.Observation map(BusHookEvent busEvent) {
        if (busEvent instanceof BusLifecycleHookEvent lifecycle) {
            return new MonitoringProtocol.Observation(
                    sequence.incrementAndGet(), lifecycle.occurredAtUtc(), "bus_" + lifecycle.state(), true,
                    null, null, null, lifecycle.busAddress(), null, null, null, null, null, null, null);
        }
        if (busEvent instanceof MessageOperationHookEvent operation) {
            return new MonitoringProtocol.Observation(
                    sequence.incrementAndGet(), operation.occurredAtUtc(), operation.kind(), operation.succeeded(),
                    operation.messageType(), operation.messageUrn(), operation.endpointName(), operation.destinationAddress(),
                    operation.durationMs(), operation.exceptionType(), operation.exceptionMessage(), operation.correlationId(),
                    operation.conversationId(), operation.traceId(), operation.spanId());
        }
        return null;
    }

    @Override
    public void close() {
        if (!closed.compareAndSet(false, true)) {
            return;
        }
        worker.shutdown();
        try {
            worker.awaitTermination(2, TimeUnit.SECONDS);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
        exportSafely();
    }
}
