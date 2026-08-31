package com.myservicebus;

import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CancellationException;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.CompletionStage;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.tasks.CancellationRegistration;
import com.myservicebus.tasks.CancellationTokenSource;

final class InMemoryJobService implements JobProvider {
    private static final class Entry {
        final UUID jobId;
        final Object job;
        final JobConsumerRegistry.Descriptor descriptor;
        final Instant submittedAtUtc;
        final Instant scheduledForUtc;
        final UUID recurringJobOccurrenceId;
        final List<JobAttemptState> attempts = new ArrayList<>();
        JobStatus status;
        Instant startedAtUtc;
        Instant completedAtUtc;
        Instant updatedAtUtc;
        JobProgress progress;
        CancellationTokenSource cancellation = new CancellationTokenSource();
        UUID scheduleToken;

        Entry(UUID jobId, Object job, JobConsumerRegistry.Descriptor descriptor,
                Instant submittedAtUtc, Instant scheduledForUtc, UUID recurringJobOccurrenceId, JobStatus status) {
            this.jobId = jobId;
            this.job = job;
            this.descriptor = descriptor;
            this.submittedAtUtc = submittedAtUtc;
            this.scheduledForUtc = scheduledForUtc;
            this.recurringJobOccurrenceId = recurringJobOccurrenceId;
            this.status = status;
            this.updatedAtUtc = submittedAtUtc;
        }
    }

    private final Map<UUID, Entry> jobs = new ConcurrentHashMap<>();
    private final JobConsumerRegistry registry;
    private final ServiceProvider services;
    private final LocalDelayScheduler delayScheduler;

    InMemoryJobService(JobConsumerRegistry registry, ServiceProvider services, LocalDelayScheduler delayScheduler) {
        this.registry = registry;
        this.services = services;
        this.delayScheduler = delayScheduler;
    }

    @Override
    public String getProviderName() {
        return "in-memory";
    }

    @Override
    public SchedulingDurability getDurability() {
        return SchedulingDurability.VOLATILE;
    }

    @Override
    public SchedulingPlacement getPlacement() {
        return SchedulingPlacement.PROCESS_LOCAL;
    }

    @Override
    public boolean isAuthoritative() {
        return true;
    }

    @Override
    public <TJob> CompletionStage<JobSubmissionReceipt> submit(
            TJob job,
            JobSubmissionOptions options,
            CancellationToken cancellationToken) {
        requireJob(job);
        cancellationToken.throwIfCancelled();
        Instant now = Instant.now();
        Entry entry = createEntry(job, options, now, null, JobStatus.WAITING);
        CompletableFuture.runAsync(() -> execute(entry));
        return CompletableFuture.completedFuture(receipt(entry));
    }

    @Override
    public <TJob> CompletionStage<JobSubmissionReceipt> schedule(
            Instant startAtUtc,
            TJob job,
            JobSubmissionOptions options,
            CancellationToken cancellationToken) {
        requireJob(job);
        if (startAtUtc == null) {
            throw new IllegalArgumentException("startAtUtc must not be null");
        }
        cancellationToken.throwIfCancelled();
        Instant now = Instant.now();
        Entry entry = createEntry(job, options, now, startAtUtc, JobStatus.SCHEDULED);
        return delayScheduler.schedule(startAtUtc, ignored -> {
            synchronized (entry) {
                if (entry.status == JobStatus.CANCELLED) {
                    return CompletableFuture.completedFuture(null);
                }
                entry.status = JobStatus.WAITING;
                entry.updatedAtUtc = Instant.now();
            }
            return CompletableFuture.runAsync(() -> execute(entry));
        }, CancellationToken.none()).thenApply(token -> {
            entry.scheduleToken = token;
            return receipt(entry);
        });
    }

    @Override
    public CompletionStage<JobControlResult> cancel(UUID jobId, CancellationToken cancellationToken) {
        cancellationToken.throwIfCancelled();
        Entry entry = jobs.get(jobId);
        if (entry == null) {
            return CompletableFuture.completedFuture(new JobControlResult(JobControlOutcome.NOT_FOUND, null));
        }
        UUID scheduleToken;
        synchronized (entry) {
            if (isTerminal(entry.status)) {
                return CompletableFuture.completedFuture(
                        new JobControlResult(JobControlOutcome.UNCHANGED, entry.status));
            }
            entry.cancellation.cancel();
            scheduleToken = entry.scheduleToken;
            if (entry.status == JobStatus.SCHEDULED || entry.status == JobStatus.WAITING) {
                complete(entry, JobStatus.CANCELLED);
            }
        }
        CompletionStage<Boolean> cancellation = scheduleToken == null
                ? CompletableFuture.completedFuture(false)
                : delayScheduler.cancel(scheduleToken);
        return cancellation.thenApply(ignored -> new JobControlResult(JobControlOutcome.APPLIED, entry.status));
    }

    @Override
    public CompletionStage<JobControlResult> retry(UUID jobId, CancellationToken cancellationToken) {
        cancellationToken.throwIfCancelled();
        Entry entry = jobs.get(jobId);
        if (entry == null) {
            return CompletableFuture.completedFuture(new JobControlResult(JobControlOutcome.NOT_FOUND, null));
        }
        synchronized (entry) {
            if (entry.status != JobStatus.FAULTED && entry.status != JobStatus.CANCELLED) {
                return CompletableFuture.completedFuture(
                        new JobControlResult(JobControlOutcome.INVALID_STATE, entry.status));
            }
            entry.cancellation = new CancellationTokenSource();
            entry.status = JobStatus.WAITING;
            entry.completedAtUtc = null;
            entry.updatedAtUtc = Instant.now();
        }
        CompletableFuture.runAsync(() -> execute(entry));
        return CompletableFuture.completedFuture(new JobControlResult(JobControlOutcome.APPLIED, JobStatus.WAITING));
    }

    @Override
    public CompletionStage<List<JobState>> getSnapshot(int maximumCount, CancellationToken cancellationToken) {
        requireMaximum(maximumCount);
        cancellationToken.throwIfCancelled();
        return CompletableFuture.completedFuture(jobs.values().stream()
                .sorted(Comparator.comparing((Entry entry) -> entry.updatedAtUtc).reversed())
                .limit(maximumCount)
                .map(this::state)
                .toList());
    }

    @Override
    public CompletionStage<List<JobAttemptState>> getAttempts(
            UUID jobId,
            int maximumCount,
            CancellationToken cancellationToken) {
        requireMaximum(maximumCount);
        cancellationToken.throwIfCancelled();
        Entry entry = jobs.get(jobId);
        if (entry == null) {
            return CompletableFuture.completedFuture(List.of());
        }
        synchronized (entry) {
            int start = Math.max(0, entry.attempts.size() - maximumCount);
            return CompletableFuture.completedFuture(List.copyOf(entry.attempts.subList(start, entry.attempts.size())));
        }
    }

    private Entry createEntry(Object job, JobSubmissionOptions options, Instant submittedAtUtc,
            Instant scheduledForUtc, JobStatus status) {
        JobSubmissionOptions effectiveOptions = options == null ? new JobSubmissionOptions() : options;
        UUID jobId = effectiveOptions.jobId() == null ? UUID.randomUUID() : effectiveOptions.jobId();
        Entry entry = new Entry(
                jobId,
                job,
                registry.get(job.getClass()),
                submittedAtUtc,
                scheduledForUtc,
                effectiveOptions.recurringJobOccurrenceId(),
                status);
        if (jobs.putIfAbsent(jobId, entry) != null) {
            throw new IllegalStateException("Job '" + jobId + "' already exists");
        }
        return entry;
    }

    private void execute(Entry entry) {
        JobConsumerRegistry.Descriptor descriptor = entry.descriptor;
        for (int retryAttempt = 0;; retryAttempt++) {
            boolean acquired = false;
            try {
                descriptor.concurrency().acquire();
                acquired = true;
                if (entry.cancellation.isCancelled()) {
                    synchronized (entry) {
                        complete(entry, JobStatus.CANCELLED);
                    }
                    return;
                }

                Instant startedAtUtc = Instant.now();
                UUID attemptId = UUID.randomUUID();
                synchronized (entry) {
                    entry.status = JobStatus.RUNNING;
                    if (entry.startedAtUtc == null) {
                        entry.startedAtUtc = startedAtUtc;
                    }
                    entry.updatedAtUtc = startedAtUtc;
                    entry.attempts.add(new JobAttemptState(
                            attemptId, entry.jobId, retryAttempt, JobAttemptStatus.RUNNING,
                            startedAtUtc, null, null, null));
                }

                CancellationTokenSource attemptCancellation = new CancellationTokenSource();
                try (CancellationRegistration registration = entry.cancellation.token()
                        .onCancel(attemptCancellation::cancel);
                        ServiceScope scope = services.createScope()) {
                    JobExecutionContext context = new JobExecutionContext(
                            entry.jobId,
                            attemptId,
                            retryAttempt,
                            entry.job,
                            startedAtUtc,
                            attemptCancellation.token(),
                            progress -> {
                                synchronized (entry) {
                                    entry.progress = progress;
                                    entry.updatedAtUtc = Instant.now();
                                }
                            });
                    CompletionStage<Void> stage = descriptor.run(scope.getServiceProvider(), context);
                    scope.detach();
                    stage.toCompletableFuture().get(
                            descriptor.options().getJobTimeout().toMillis(), TimeUnit.MILLISECONDS);
                    synchronized (entry) {
                        finishAttempt(entry, attemptId, JobAttemptStatus.COMPLETED, null);
                        complete(entry, JobStatus.COMPLETED);
                    }
                    return;
                } catch (CancellationException exception) {
                    synchronized (entry) {
                        finishAttempt(entry, attemptId, JobAttemptStatus.CANCELLED, null);
                        complete(entry, JobStatus.CANCELLED);
                    }
                    return;
                } catch (Exception exception) {
                    Throwable failure = unwrap(exception);
                    if (entry.cancellation.isCancelled()) {
                        synchronized (entry) {
                            finishAttempt(entry, attemptId, JobAttemptStatus.CANCELLED, null);
                            complete(entry, JobStatus.CANCELLED);
                        }
                        return;
                    }
                    if (failure instanceof TimeoutException) {
                        attemptCancellation.cancel();
                    }
                    synchronized (entry) {
                        finishAttempt(entry, attemptId, JobAttemptStatus.FAULTED, failure);
                    }
                    if (retryAttempt >= descriptor.options().getRetryCount()) {
                        synchronized (entry) {
                            complete(entry, JobStatus.FAULTED);
                        }
                        return;
                    }
                    synchronized (entry) {
                        entry.status = JobStatus.WAITING;
                        entry.updatedAtUtc = Instant.now();
                    }
                    Duration delay = descriptor.options().getRetryDelay();
                    if (delay != null && !delay.isZero()) {
                        Thread.sleep(delay.toMillis());
                    }
                }
            } catch (InterruptedException exception) {
                Thread.currentThread().interrupt();
                synchronized (entry) {
                    complete(entry, JobStatus.CANCELLED);
                }
                return;
            } finally {
                if (acquired) {
                    descriptor.concurrency().release();
                }
            }
        }
    }

    private void finishAttempt(Entry entry, UUID attemptId, JobAttemptStatus status, Throwable failure) {
        for (int index = 0; index < entry.attempts.size(); index++) {
            JobAttemptState current = entry.attempts.get(index);
            if (current.attemptId().equals(attemptId)) {
                entry.attempts.set(index, new JobAttemptState(
                        current.attemptId(), current.jobId(), current.retryAttempt(), status,
                        current.startedAtUtc(), Instant.now(),
                        failure == null ? null : failure.getClass().getName(),
                        failure == null ? null : failure.getMessage()));
                return;
            }
        }
    }

    private void complete(Entry entry, JobStatus status) {
        Instant now = Instant.now();
        entry.status = status;
        entry.completedAtUtc = now;
        entry.updatedAtUtc = now;
    }

    private JobSubmissionReceipt receipt(Entry entry) {
        return new JobSubmissionReceipt(
                entry.jobId, entry.status, entry.submittedAtUtc, entry.scheduledForUtc);
    }

    private JobState state(Entry entry) {
        synchronized (entry) {
            return new JobState(
                    entry.jobId,
                    entry.descriptor.jobTypeName(),
                    entry.status,
                    getProviderName(),
                    getDurability(),
                    getPlacement(),
                    entry.submittedAtUtc,
                    entry.scheduledForUtc,
                    entry.startedAtUtc,
                    entry.completedAtUtc,
                    entry.progress,
                    entry.recurringJobOccurrenceId,
                    entry.updatedAtUtc);
        }
    }

    private static Throwable unwrap(Throwable failure) {
        if ((failure instanceof ExecutionException || failure instanceof CompletionException)
                && failure.getCause() != null) {
            return failure.getCause();
        }
        return failure;
    }

    private static void requireJob(Object job) {
        if (job == null) {
            throw new IllegalArgumentException("job must not be null");
        }
    }

    private static void requireMaximum(int maximumCount) {
        if (maximumCount <= 0) {
            throw new IllegalArgumentException("maximumCount must be greater than zero");
        }
    }

    private static boolean isTerminal(JobStatus status) {
        return status == JobStatus.COMPLETED || status == JobStatus.FAULTED || status == JobStatus.CANCELLED;
    }
}
