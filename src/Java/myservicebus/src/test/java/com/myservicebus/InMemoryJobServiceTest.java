package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.Instant;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CancellationException;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.function.Consumer;

import javax.inject.Inject;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;

class InMemoryJobServiceTest {
    @Test
    void executesRegisteredJobAndReportsProgress() throws Exception {
        ServiceProvider provider = createProvider(ProgressJobConsumer.class, ProgressJob.class, null, new JobRecorder());
        JobClient client = provider.getRequiredService(JobClient.class);
        JobSource source = provider.getRequiredService(JobSource.class);

        JobSubmissionReceipt receipt = client.submit(new ProgressJob(7)).toCompletableFuture().get();
        JobState state = waitForState(source, receipt.jobId(), JobStatus.COMPLETED);

        assertEquals(ProgressJob.class.getSimpleName(), state.jobType());
        assertEquals(new JobProgress(7, 10L), state.progress());
        List<JobAttemptState> attempts = source.getAttempts(receipt.jobId(), 10).toCompletableFuture().get();
        assertEquals(1, attempts.size());
        assertEquals(JobAttemptStatus.COMPLETED, attempts.get(0).status());
    }

    @Test
    void retriesFaultedAttempt() throws Exception {
        JobRecorder recorder = new JobRecorder();
        ServiceProvider provider = createProvider(
                RetryJobConsumer.class,
                RetryJob.class,
                options -> options.setRetry(retry -> retry.immediate(2)),
                recorder);
        JobClient client = provider.getRequiredService(JobClient.class);
        JobSource source = provider.getRequiredService(JobSource.class);

        JobSubmissionReceipt receipt = client.submit(new RetryJob()).toCompletableFuture().get();
        waitForState(source, receipt.jobId(), JobStatus.COMPLETED);

        assertEquals(3, recorder.attempts.get());
        assertEquals(
                List.of(JobAttemptStatus.FAULTED, JobAttemptStatus.FAULTED, JobAttemptStatus.COMPLETED),
                source.getAttempts(receipt.jobId(), 10).toCompletableFuture().get().stream()
                        .map(JobAttemptState::status)
                        .toList());
    }

    @Test
    void cancelsScheduledWorkBeforeItStarts() throws Exception {
        JobRecorder recorder = new JobRecorder();
        ServiceProvider provider = createProvider(
                CountingJobConsumer.class, CountingJob.class, null, recorder);
        JobClient client = provider.getRequiredService(JobClient.class);
        JobSource source = provider.getRequiredService(JobSource.class);

        JobSubmissionReceipt receipt = client.schedule(Instant.now().plusSeconds(60), new CountingJob())
                .toCompletableFuture().get();
        JobControlResult result = client.cancel(receipt.jobId()).toCompletableFuture().get();

        assertEquals(JobControlOutcome.APPLIED, result.outcome());
        assertEquals(JobStatus.CANCELLED, waitForState(source, receipt.jobId(), JobStatus.CANCELLED).status());
        assertEquals(0, recorder.attempts.get());
    }

    @Test
    void enforcesPerConsumerConcurrencyLimit() throws Exception {
        JobRecorder recorder = new JobRecorder();
        ServiceProvider provider = createProvider(
                ConcurrentJobConsumer.class,
                ConcurrentJob.class,
                options -> options.setConcurrentJobLimit(1),
                recorder);
        JobClient client = provider.getRequiredService(JobClient.class);
        JobSource source = provider.getRequiredService(JobSource.class);

        JobSubmissionReceipt first = client.submit(new ConcurrentJob()).toCompletableFuture().get();
        JobSubmissionReceipt second = client.submit(new ConcurrentJob()).toCompletableFuture().get();
        waitForState(source, first.jobId(), JobStatus.COMPLETED);
        waitForState(source, second.jobId(), JobStatus.COMPLETED);

        assertEquals(1, recorder.maximumConcurrency.get());
    }

    @Test
    void timesOutAndRetriesLongRunningWork() throws Exception {
        ServiceProvider provider = createProvider(
                TimeoutJobConsumer.class,
                TimeoutJob.class,
                options -> options
                        .setJobTimeout(java.time.Duration.ofMillis(20))
                        .setRetry(retry -> retry.immediate(1)),
                new JobRecorder());
        JobClient client = provider.getRequiredService(JobClient.class);
        JobSource source = provider.getRequiredService(JobSource.class);

        JobSubmissionReceipt receipt = client.submit(new TimeoutJob()).toCompletableFuture().get();
        waitForState(source, receipt.jobId(), JobStatus.FAULTED);

        List<JobAttemptState> attempts = source.getAttempts(receipt.jobId(), 10).toCompletableFuture().get();
        assertEquals(2, attempts.size());
        assertEquals(List.of(
                java.util.concurrent.TimeoutException.class.getName(),
                java.util.concurrent.TimeoutException.class.getName()),
                attempts.stream().map(JobAttemptState::faultType).toList());
    }

    @Test
    void manuallyRetriesTerminalJob() throws Exception {
        JobRecorder recorder = new JobRecorder();
        ServiceProvider provider = createProvider(
                ManualRetryJobConsumer.class, ManualRetryJob.class, null, recorder);
        JobClient client = provider.getRequiredService(JobClient.class);
        JobSource source = provider.getRequiredService(JobSource.class);

        JobSubmissionReceipt receipt = client.submit(new ManualRetryJob()).toCompletableFuture().get();
        waitForState(source, receipt.jobId(), JobStatus.FAULTED);
        assertEquals(JobControlOutcome.APPLIED,
                client.retry(receipt.jobId()).toCompletableFuture().get().outcome());
        waitForState(source, receipt.jobId(), JobStatus.COMPLETED);

        assertEquals(2, recorder.attempts.get());
    }

    private static <TJob, TConsumer extends JobConsumer<TJob>> ServiceProvider createProvider(
            Class<TConsumer> consumerClass,
            Class<TJob> jobClass,
            Consumer<JobConsumerOptions> configure,
            JobRecorder recorder) {
        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(JobRecorder.class, () -> recorder);
        services.from(MessageBusServices.class).addServiceBus(configurator ->
                configurator.addJobConsumer(consumerClass, jobClass, configure));
        return services.buildServiceProvider();
    }

    private static JobState waitForState(JobSource source, java.util.UUID jobId, JobStatus expected)
            throws Exception {
        Instant timeout = Instant.now().plusSeconds(5);
        while (Instant.now().isBefore(timeout)) {
            JobState state = source.getSnapshot(100).toCompletableFuture().get().stream()
                    .filter(job -> job.jobId().equals(jobId))
                    .findFirst()
                    .orElseThrow();
            if (state.status() == expected) {
                return state;
            }
            Thread.sleep(10);
        }
        throw new java.util.concurrent.TimeoutException("Job did not reach " + expected);
    }

    record ProgressJob(int value) {
    }

    static final class ProgressJobConsumer implements JobConsumer<ProgressJob> {
        @Override
        public java.util.concurrent.CompletionStage<Void> run(JobContext<ProgressJob> context) {
            return context.setProgress(context.getJob().value(), 10L);
        }
    }

    record RetryJob() {
    }

    static final class RetryJobConsumer implements JobConsumer<RetryJob> {
        private final JobRecorder recorder;

        @Inject
        RetryJobConsumer(JobRecorder recorder) {
            this.recorder = recorder;
        }

        @Override
        public java.util.concurrent.CompletionStage<Void> run(JobContext<RetryJob> context) {
            if (recorder.attempts.incrementAndGet() < 3) {
                return CompletableFuture.failedFuture(new IllegalStateException("Try again"));
            }
            return CompletableFuture.completedFuture(null);
        }
    }

    record CountingJob() {
    }

    static final class CountingJobConsumer implements JobConsumer<CountingJob> {
        private final JobRecorder recorder;

        @Inject
        CountingJobConsumer(JobRecorder recorder) {
            this.recorder = recorder;
        }

        @Override
        public java.util.concurrent.CompletionStage<Void> run(JobContext<CountingJob> context) {
            recorder.attempts.incrementAndGet();
            return CompletableFuture.completedFuture(null);
        }
    }

    record ConcurrentJob() {
    }

    static final class ConcurrentJobConsumer implements JobConsumer<ConcurrentJob> {
        private final JobRecorder recorder;

        @Inject
        ConcurrentJobConsumer(JobRecorder recorder) {
            this.recorder = recorder;
        }

        @Override
        public java.util.concurrent.CompletionStage<Void> run(JobContext<ConcurrentJob> context) {
            int current = recorder.concurrency.incrementAndGet();
            recorder.maximumConcurrency.accumulateAndGet(current, Math::max);
            return CompletableFuture.runAsync(() -> {
                try {
                    Thread.sleep(30);
                } catch (InterruptedException exception) {
                    Thread.currentThread().interrupt();
                } finally {
                    recorder.concurrency.decrementAndGet();
                }
            });
        }
    }

    record TimeoutJob() {
    }

    static final class TimeoutJobConsumer implements JobConsumer<TimeoutJob> {
        @Override
        public java.util.concurrent.CompletionStage<Void> run(JobContext<TimeoutJob> context) {
            CompletableFuture<Void> result = new CompletableFuture<>();
            context.getCancellationToken().onCancel(() ->
                    result.completeExceptionally(new CancellationException()));
            return result;
        }
    }

    record ManualRetryJob() {
    }

    static final class ManualRetryJobConsumer implements JobConsumer<ManualRetryJob> {
        private final JobRecorder recorder;

        @Inject
        ManualRetryJobConsumer(JobRecorder recorder) {
            this.recorder = recorder;
        }

        @Override
        public java.util.concurrent.CompletionStage<Void> run(JobContext<ManualRetryJob> context) {
            if (recorder.attempts.incrementAndGet() == 1) {
                return CompletableFuture.failedFuture(new IllegalStateException("Retry manually"));
            }
            return CompletableFuture.completedFuture(null);
        }
    }

    static final class JobRecorder {
        final AtomicInteger attempts = new AtomicInteger();
        final AtomicInteger concurrency = new AtomicInteger();
        final AtomicInteger maximumConcurrency = new AtomicInteger();
    }
}
