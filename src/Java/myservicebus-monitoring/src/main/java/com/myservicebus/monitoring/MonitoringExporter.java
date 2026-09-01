package com.myservicebus.monitoring;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.LinkedHashMap;
import java.util.Map;
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
import com.myservicebus.OutboxDeliveryHookEvent;
import com.myservicebus.RecurringJobSource;
import com.myservicebus.RecurringJobState;
import com.myservicebus.JobAttemptState;
import com.myservicebus.JobSource;
import com.myservicebus.JobState;
import com.myservicebus.ScheduledWorkObserver;
import com.myservicebus.ScheduledWorkSource;
import com.myservicebus.ScheduledWorkState;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.inspection.BusInspectionProvider;

public final class MonitoringExporter implements BusHook, ScheduledWorkObserver, AutoCloseable {
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
    private final AtomicBoolean scheduledWorkChanged = new AtomicBoolean(true);
    private final Map<String, MonitoringProtocol.ScheduledWorkItem> scheduledWork = new LinkedHashMap<>();
    private final Instant startedAtUtc = Instant.now();
    private volatile BusInspectionProvider inspectionProvider;
    private volatile List<ScheduledWorkSource> scheduledWorkSources = List.of();
    private volatile List<RecurringJobSource> recurringJobSources = List.of();
    private volatile List<MonitoringProtocol.RecurringJobItem> recurringJobs = List.of();
    private volatile List<JobSource> jobSources = List.of();
    private volatile List<MonitoringProtocol.JobItem> jobs = List.of();
    private volatile boolean metadataRegistered;
    private boolean scheduledWorkSourcesAvailable = true;
    private Instant nextScheduledWorkRefresh = Instant.MIN;
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
        if (options.getMaxScheduledWorkItems() <= 0 || options.getMaxJobItems() <= 0
                || options.getMaxJobAttempts() <= 0 || options.getScheduledWorkHistory().isZero()
                || options.getScheduledWorkHistory().isNegative()) {
            throw new IllegalArgumentException("Scheduled work limits must be greater than zero");
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

    public void start(ServiceProvider serviceProvider) {
        ScheduledWorkSource source = serviceProvider.getService(ScheduledWorkSource.class);
        RecurringJobSource recurringSource = serviceProvider.getService(RecurringJobSource.class);
        JobSource jobSource = serviceProvider.getService(JobSource.class);
        start(
                serviceProvider.getRequiredService(BusInspectionProvider.class),
                source == null ? List.of() : List.of(source),
                recurringSource == null ? List.of() : List.of(recurringSource),
                jobSource == null ? List.of() : List.of(jobSource));
    }

    public void start(BusInspectionProvider inspectionProvider, List<ScheduledWorkSource> scheduledWorkSources) {
        start(inspectionProvider, scheduledWorkSources, List.of());
    }

    public void start(
            BusInspectionProvider inspectionProvider,
            List<ScheduledWorkSource> scheduledWorkSources,
            List<RecurringJobSource> recurringJobSources) {
        start(inspectionProvider, scheduledWorkSources, recurringJobSources, List.of());
    }

    public void start(
            BusInspectionProvider inspectionProvider,
            List<ScheduledWorkSource> scheduledWorkSources,
            List<RecurringJobSource> recurringJobSources,
            List<JobSource> jobSources) {
        this.scheduledWorkSources = List.copyOf(scheduledWorkSources);
        this.recurringJobSources = List.copyOf(recurringJobSources);
        this.jobSources = List.copyOf(jobSources);
        start(inspectionProvider);
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

    @Override
    public void observe(ScheduledWorkState state) {
        synchronized (scheduledWork) {
            pruneScheduledWork(Instant.now());
            scheduledWork.put(state.tokenId().toString(), mapScheduledWork(state));
            while (scheduledWork.size() > options.getMaxScheduledWorkItems()) {
                String oldest = scheduledWork.entrySet().stream()
                        .min(java.util.Comparator.comparing(entry -> entry.getValue().updatedAtUtc()))
                        .orElseThrow().getKey();
                scheduledWork.remove(oldest);
            }
        }
        scheduledWorkChanged.set(true);
        if (started.get() && !closed.get()) {
            worker.execute(this::exportSafely);
        }
    }

    private void exportSafely() {
        try {
            ensureMetadata();
            Instant now = Instant.now();
            if (!now.isBefore(nextScheduledWorkRefresh)) {
                nextScheduledWorkRefresh = now.plus(options.getExportInterval());
                refreshScheduledWork();
                refreshRecurringJobs();
                refreshJobs();
                scheduledWorkChanged.set(true);
            }
            if (metadataRegistered && scheduledWorkSourcesAvailable && scheduledWorkChanged.getAndSet(false)) {
                try {
                    if (!sendScheduledWork()) {
                        scheduledWorkChanged.set(true);
                    }
                    if (!sendRecurringJobs()) {
                        scheduledWorkChanged.set(true);
                    }
                    if (!sendJobs()) {
                        scheduledWorkChanged.set(true);
                    }
                } catch (Exception exception) {
                    scheduledWorkChanged.set(true);
                    throw exception;
                }
            }
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
                provider.getSnapshot(),
                java.util.Map.copyOf(options.getLabels()));
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

    private boolean sendScheduledWork() throws Exception {
        List<MonitoringProtocol.ScheduledWorkItem> items;
        synchronized (scheduledWork) {
            pruneScheduledWork(Instant.now());
            items = List.copyOf(scheduledWork.values());
        }
        return post("/api/monitoring/v1/scheduled-work", new MonitoringProtocol.ScheduledWorkSnapshot(
                MonitoringProtocol.VERSION, options.getApplicationName(), options.getInstanceId(), options.getBusId(),
                Instant.now(), items));
    }

    private void refreshScheduledWork() {
        try {
            for (ScheduledWorkSource source : scheduledWorkSources) {
                List<ScheduledWorkState> states = source.getSnapshot(options.getMaxScheduledWorkItems())
                        .toCompletableFuture().join();
                java.util.Set<String> tokenIds = states.stream()
                        .map(state -> state.tokenId().toString())
                        .collect(java.util.stream.Collectors.toSet());
                synchronized (scheduledWork) {
                    if (source.isAuthoritative()) {
                        scheduledWork.entrySet().removeIf(entry -> entry.getValue().provider().equals(source.getProvider())
                                && !isTerminal(entry.getValue().status())
                                && !tokenIds.contains(entry.getKey()));
                    }
                    for (ScheduledWorkState state : states) {
                        scheduledWork.put(state.tokenId().toString(), mapScheduledWork(state));
                    }
                }
            }
            scheduledWorkSourcesAvailable = true;
        } catch (RuntimeException failure) {
            scheduledWorkSourcesAvailable = false;
            throw failure;
        }
    }

    private void refreshRecurringJobs() {
        List<MonitoringProtocol.RecurringJobItem> items = new ArrayList<>();
        for (RecurringJobSource source : recurringJobSources) {
            source.getSnapshot(options.getMaxScheduledWorkItems()).toCompletableFuture().join().stream()
                    .map(MonitoringExporter::mapRecurringJob)
                    .forEach(items::add);
        }
        recurringJobs = items.stream()
                .sorted(java.util.Comparator.comparing(
                        MonitoringProtocol.RecurringJobItem::nextOccurrenceAtUtc,
                        java.util.Comparator.nullsLast(java.util.Comparator.naturalOrder())))
                .limit(options.getMaxScheduledWorkItems())
                .toList();
    }

    private boolean sendRecurringJobs() throws Exception {
        return post("/api/monitoring/v1/recurring-jobs", new MonitoringProtocol.RecurringJobSnapshot(
                MonitoringProtocol.VERSION,
                options.getApplicationName(),
                options.getInstanceId(),
                options.getBusId(),
                Instant.now(),
                recurringJobs));
    }

    private void refreshJobs() {
        List<MonitoringProtocol.JobItem> items = new ArrayList<>();
        for (JobSource source : jobSources) {
            for (JobState state : source.getSnapshot(options.getMaxJobItems()).toCompletableFuture().join()) {
                List<JobAttemptState> attempts = source.getAttempts(state.jobId(), options.getMaxJobAttempts())
                        .toCompletableFuture().join();
                items.add(mapJob(state, attempts));
            }
        }
        jobs = items.stream()
                .sorted(java.util.Comparator.comparing(MonitoringProtocol.JobItem::updatedAtUtc).reversed())
                .limit(options.getMaxJobItems())
                .toList();
    }

    private boolean sendJobs() throws Exception {
        return post("/api/monitoring/v1/jobs", new MonitoringProtocol.JobSnapshot(
                MonitoringProtocol.VERSION,
                options.getApplicationName(),
                options.getInstanceId(),
                options.getBusId(),
                Instant.now(),
                jobs));
    }

    private void pruneScheduledWork(Instant now) {
        Instant cutoff = now.minus(options.getScheduledWorkHistory());
        scheduledWork.entrySet().removeIf(entry -> isTerminal(entry.getValue().status())
                && entry.getValue().updatedAtUtc().isBefore(cutoff));
    }

    private static boolean isTerminal(String status) {
        return status.equals("Completed") || status.equals("Cancelled") || status.equals("Failed");
    }

    private static String titleCase(String value) {
        String lower = value.toLowerCase(java.util.Locale.ROOT);
        return Character.toUpperCase(lower.charAt(0)) + lower.substring(1);
    }

    private static String enumTitle(String value) {
        return java.util.Arrays.stream(value.split("_"))
                .map(MonitoringExporter::titleCase)
                .collect(java.util.stream.Collectors.joining());
    }

    private static MonitoringProtocol.ScheduledWorkItem mapScheduledWork(ScheduledWorkState state) {
        return new MonitoringProtocol.ScheduledWorkItem(
                state.tokenId().toString(), state.provider(), titleCase(state.durability().name()), state.workKind(),
                state.messageType(), state.intent(), state.destinationAddress(), state.dueAtUtc(),
                titleCase(state.status().name()), state.providerStatus(), state.attempt(), state.updatedAtUtc(),
                state.failureCategory());
    }

    private static MonitoringProtocol.RecurringJobItem mapRecurringJob(RecurringJobState state) {
        return new MonitoringProtocol.RecurringJobItem(
                state.definitionId().toString(),
                state.identity().scheduleId(),
                state.identity().scheduleGroup(),
                state.revision(),
                state.provider(),
                enumTitle(state.durability().name()),
                enumTitle(state.placement().name()),
                state.cadence(),
                state.messageType(),
                enumTitle(state.status().name()),
                state.nextOccurrenceAtUtc(),
                state.updatedAtUtc());
    }

    private static MonitoringProtocol.JobItem mapJob(JobState state, List<JobAttemptState> attempts) {
        return new MonitoringProtocol.JobItem(
                state.jobId().toString(),
                state.jobType(),
                enumTitle(state.status().name()),
                state.provider(),
                enumTitle(state.durability().name()),
                enumTitle(state.placement().name()),
                state.submittedAtUtc(),
                state.scheduledForUtc(),
                state.startedAtUtc(),
                state.completedAtUtc(),
                state.progress() == null ? null : state.progress().value(),
                state.progress() == null ? null : state.progress().limit(),
                state.recurringJobOccurrenceId() == null ? null : state.recurringJobOccurrenceId().toString(),
                state.updatedAtUtc(),
                attempts.stream().map(attempt -> new MonitoringProtocol.JobAttemptItem(
                        attempt.attemptId().toString(),
                        attempt.retryAttempt(),
                        enumTitle(attempt.status().name()),
                        attempt.startedAtUtc(),
                        attempt.completedAtUtc(),
                        attempt.faultType())).toList());
    }

    private MonitoringProtocol.Observation map(BusHookEvent busEvent) {
        if (busEvent instanceof BusLifecycleHookEvent lifecycle) {
            return new MonitoringProtocol.Observation(
                    sequence.incrementAndGet(), lifecycle.occurredAtUtc(), "bus_" + lifecycle.state(), true,
                    null, null, null, lifecycle.busAddress(), null, null, null, null, null, null, null, null, null, null,
                    null, null);
        }
        if (busEvent instanceof MessageOperationHookEvent operation) {
            return new MonitoringProtocol.Observation(
                    sequence.incrementAndGet(), operation.occurredAtUtc(), operation.kind(), operation.succeeded(),
                    operation.messageType(), operation.messageUrn(), operation.endpointName(), operation.destinationAddress(),
                    operation.durationMs(), operation.exceptionType(), operation.exceptionMessage(), operation.correlationId(),
                    operation.conversationId(), operation.traceId(), operation.spanId(), operation.retryAttempt(),
                    operation.retryLimit(), null, operation.messageId(), operation.causationMessageId());
        }
        if (busEvent instanceof OutboxDeliveryHookEvent outbox) {
            Map<String, String> properties = new LinkedHashMap<>();
            properties.put("service_name", outbox.serviceName());
            properties.put("owner_id", outbox.ownerId());
            properties.put("batch_leased", format(outbox.batchLeased()));
            properties.put("batch_dispatched", format(outbox.batchDispatched()));
            properties.put("batch_failed", format(outbox.batchFailed()));
            properties.put("batch_lost_leases", format(outbox.batchLostLeases()));
            properties.put("pending", format(outbox.pending()));
            properties.put("leased", format(outbox.leased()));
            properties.put("retrying", format(outbox.retrying()));
            properties.put("stored_dispatched", format(outbox.storedDispatched()));
            properties.put("dead", format(outbox.dead()));
            properties.put("cancelled", format(outbox.cancelled()));
            properties.put("oldest_undispatched_age_ms", format(outbox.oldestUndispatchedAgeMs()));
            return new MonitoringProtocol.Observation(
                    sequence.incrementAndGet(), outbox.occurredAtUtc(), "outbox_dispatch_cycle", outbox.succeeded(),
                    null, null, outbox.serviceName(), null, outbox.durationMs(), outbox.failureCategory(), null,
                    null, null, null, null, null, null, properties, null, null);
        }
        return null;
    }

    private static String format(Object value) {
        return value == null ? "" : value.toString();
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
