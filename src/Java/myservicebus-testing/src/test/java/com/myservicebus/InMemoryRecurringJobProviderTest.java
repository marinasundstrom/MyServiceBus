package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.mediator.MediatorTransport;
import com.myservicebus.tasks.CancellationToken;
import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.InputStream;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneId;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.function.Function;
import javax.inject.Inject;
import org.junit.jupiter.api.Test;

class InMemoryRecurringJobProviderTest {
    private record TestJob(String value) {
    }

    private static final class JobRecorder {
        private final CountDownLatch completion = new CountDownLatch(1);
        private volatile String value;
    }

    private static final class TestJobConsumer implements JobConsumer<TestJob> {
        private final JobRecorder recorder;

        @Inject
        TestJobConsumer(JobRecorder recorder) {
            this.recorder = recorder;
        }

        @Override
        public CompletionStage<Void> run(JobContext<TestJob> context) {
            recorder.value = context.getJob().value();
            recorder.completion.countDown();
            return CompletableFuture.completedFuture(null);
        }
    }

    private static final class RecordingJobClient implements JobClient {
        private final List<Object> messages = new ArrayList<>();

        @Override
        public <TJob> CompletionStage<JobSubmissionReceipt> submit(
                TJob job,
                JobSubmissionOptions options,
                CancellationToken cancellationToken) {
            messages.add(job);
            return CompletableFuture.completedFuture(new JobSubmissionReceipt(
                    options.jobId() == null ? UUID.randomUUID() : options.jobId(),
                    JobStatus.WAITING,
                    Instant.now(),
                    null));
        }

        @Override
        public <TJob> CompletionStage<JobSubmissionReceipt> schedule(
                Instant startAtUtc,
                TJob job,
                JobSubmissionOptions options,
                CancellationToken cancellationToken) {
            throw new UnsupportedOperationException();
        }

        @Override
        public CompletionStage<JobControlResult> cancel(UUID jobId, CancellationToken cancellationToken) {
            throw new UnsupportedOperationException();
        }

        @Override
        public CompletionStage<JobControlResult> retry(UUID jobId, CancellationToken cancellationToken) {
            throw new UnsupportedOperationException();
        }
    }

    private static final class ManualDelayScheduler implements LocalDelayScheduler {
        private record ScheduledCallback(
                Instant scheduledTime,
                Function<CancellationToken, CompletionStage<Void>> callback) {
        }

        private final Map<UUID, ScheduledCallback> callbacks =
                new LinkedHashMap<>();

        @Override
        public CompletionStage<UUID> schedule(
                Instant scheduledTime,
                Function<CancellationToken, CompletionStage<Void>> callback,
                CancellationToken cancellationToken) {
            UUID token = UUID.randomUUID();
            callbacks.put(token, new ScheduledCallback(scheduledTime, callback));
            return CompletableFuture.completedFuture(token);
        }

        @Override
        public CompletionStage<Boolean> cancel(UUID tokenId) {
            return CompletableFuture.completedFuture(callbacks.remove(tokenId) != null);
        }

        CompletionStage<Void> runNext() {
            Map.Entry<UUID, ScheduledCallback> next =
                    callbacks.entrySet().iterator().next();
            callbacks.remove(next.getKey());
            return next.getValue().callback().apply(CancellationToken.none());
        }

        Instant nextScheduledTime() {
            return callbacks.values().iterator().next().scheduledTime();
        }
    }

    private static final class MutableClock extends Clock {
        private Instant now;

        private MutableClock(Instant now) {
            this.now = now;
        }

        void setInstant(Instant value) {
            now = value;
        }

        @Override
        public ZoneId getZone() {
            return ZoneOffset.UTC;
        }

        @Override
        public Clock withZone(ZoneId zone) {
            return this;
        }

        @Override
        public Instant instant() {
            return now;
        }
    }

    private record FixedIntervalFixture(
            String name,
            String policy,
            int maxCatchUpOccurrences,
            Instant nowUtc,
            int expectedDispatchCount,
            Instant expectedNextUtc) {
    }

    @Test
    void addOrUpdateIsIdempotentAndRevisionsChangedContent() {
        RecordingJobClient publisher = new RecordingJobClient();
        ManualDelayScheduler delays = new ManualDelayScheduler();
        InMemoryRecurringJobProvider provider = createProvider(publisher, delays);
        RecurringJobIdentity identity = new RecurringJobIdentity("daily-export", "billing");
        RecurringJobDefinition definition = new RecurringJobDefinition(
                identity,
                new FixedIntervalRecurringJobCadence(Duration.ofHours(1)));
        TestJob job = new TestJob("first");

        RecurringJobDefinitionReceipt first = provider.addOrUpdate(definition, job)
                .toCompletableFuture().join();
        RecurringJobDefinitionReceipt repeated = provider.addOrUpdate(definition, job)
                .toCompletableFuture().join();

        assertEquals(first.definitionId(), repeated.definitionId());
        assertEquals(1, repeated.revision());
        assertEquals(SchedulingDurability.VOLATILE, repeated.durability());
        assertEquals(SchedulingPlacement.PROCESS_LOCAL, repeated.placement());
        assertEquals(Instant.parse("2026-09-01T01:00:00Z"), repeated.nextOccurrenceAtUtc());
        assertEquals(1, delays.callbacks.size());

        RecurringJobDefinitionReceipt changed = provider.addOrUpdate(
                definition,
                new TestJob("changed"),
                1L).toCompletableFuture().join();

        assertEquals(first.definitionId(), changed.definitionId());
        assertEquals(2, changed.revision());
        assertEquals(1, delays.callbacks.size());
    }

    @Test
    void revisionConflictsAndControlsAreExplicit() {
        ManualDelayScheduler delays = new ManualDelayScheduler();
        InMemoryRecurringJobProvider provider = createProvider(new RecordingJobClient(), delays);
        RecurringJobIdentity identity = new RecurringJobIdentity("daily-export");
        RecurringJobDefinition definition = new RecurringJobDefinition(
                identity,
                new FixedIntervalRecurringJobCadence(Duration.ofHours(1)));
        provider.addOrUpdate(definition, new TestJob("first")).toCompletableFuture().join();

        RecurringJobRevisionConflictException conflict = assertThrows(
                RecurringJobRevisionConflictException.class,
                () -> provider.pause(identity, 99L).toCompletableFuture().join());
        assertEquals(1, conflict.getCurrentRevision());

        RecurringJobControlResult paused = provider.pause(identity, 1L).toCompletableFuture().join();
        assertEquals(RecurringJobControlOutcome.APPLIED, paused.outcome());
        assertEquals(2, paused.currentRevision());
        assertEquals(0, delays.callbacks.size());

        RecurringJobControlResult resumed = provider.resume(identity, 2L).toCompletableFuture().join();
        assertEquals(3, resumed.currentRevision());
        assertEquals(1, delays.callbacks.size());

        RecurringJobControlResult removed = provider.remove(identity, 3L).toCompletableFuture().join();
        assertEquals(4, removed.currentRevision());
        assertEquals(0, delays.callbacks.size());
        assertEquals(RecurringJobControlOutcome.NOT_FOUND,
                provider.remove(identity).toCompletableFuture().join().outcome());
    }

    @Test
    void dueAndManualOccurrencesSubmitTrackedJobs() {
        RecordingJobClient publisher = new RecordingJobClient();
        ManualDelayScheduler delays = new ManualDelayScheduler();
        InMemoryRecurringJobProvider provider = createProvider(publisher, delays);
        RecurringJobIdentity identity = new RecurringJobIdentity("daily-export");
        provider.addOrUpdate(
                new RecurringJobDefinition(
                        identity,
                        new FixedIntervalRecurringJobCadence(Duration.ofHours(1))),
                new TestJob("run")).toCompletableFuture().join();

        delays.runNext().toCompletableFuture().join();
        assertEquals(1, publisher.messages.size());
        assertEquals(1, delays.callbacks.size());

        RecurringJobOccurrenceReceipt manual = provider.triggerNow(identity)
                .toCompletableFuture().join();
        assertTrue(manual.manual());
        assertEquals(RecurringJobOccurrenceStatus.PENDING, manual.status());
        assertEquals(2, publisher.messages.size());
        assertEquals(1, delays.callbacks.size());
    }

    @Test
    void defaultRegistrationExecutesManualOccurrenceAsTrackedJob() throws Exception {
        JobRecorder recorder = new JobRecorder();
        ServiceCollection registrations = ServiceCollection.create();
        registrations.addSingleton(JobRecorder.class, () -> recorder);
        registrations.from(MessageBusServices.class).addServiceBus(configurator -> {
            configurator.addJobConsumer(TestJobConsumer.class, TestJob.class, null);
            MediatorTransport.configure(configurator);
        });
        ServiceProvider services = registrations.buildServiceProvider();
        RecurringJobProvider recurring = services.getRequiredService(RecurringJobProvider.class);
        RecurringJobIdentity identity = new RecurringJobIdentity("tracked-manual");
        recurring.addOrUpdate(
                new RecurringJobDefinition(
                        identity,
                        new FixedIntervalRecurringJobCadence(Duration.ofHours(1))),
                new TestJob("executed")).toCompletableFuture().join();

        RecurringJobOccurrenceReceipt occurrence = recurring.triggerNow(identity)
                .toCompletableFuture().join();
        assertTrue(recorder.completion.await(5, TimeUnit.SECONDS));
        JobState job = services.getRequiredService(JobSource.class)
                .getSnapshot(10).toCompletableFuture().join().get(0);

        assertEquals("executed", recorder.value);
        assertEquals(occurrence.occurrenceId(), job.recurringJobOccurrenceId());
        assertEquals(JobStatus.COMPLETED, job.status());
    }

    @Test
    void unsupportedCadenceAndOverlapAreRejected() {
        InMemoryRecurringJobProvider provider = createProvider(
                new RecordingJobClient(),
                new ManualDelayScheduler());

        assertThrows(UnsupportedOperationException.class, () -> provider.addOrUpdate(
                new RecurringJobDefinition(
                        new RecurringJobIdentity("cron"),
                        new CronRecurringJobCadence("0 1 * * *", RecurringJobCronDialect.UNIX5)),
                new TestJob("run")));
        assertThrows(UnsupportedOperationException.class, () -> provider.addOrUpdate(
                new RecurringJobDefinition(
                        new RecurringJobIdentity("serial"),
                        new FixedIntervalRecurringJobCadence(Duration.ofHours(1)),
                        null, null, null,
                        RecurringJobMisfirePolicy.FIRE_ONCE_NOW,
                        1,
                        RecurringJobOverlapPolicy.FORBID),
                new TestJob("run")));
    }

    @Test
    void addServiceBusPreservesAnExplicitProviderRegistration() {
        InMemoryRecurringJobProvider custom = createProvider(
                new RecordingJobClient(),
                new ManualDelayScheduler());
        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(RecurringJobProvider.class, () -> custom);
        services.from(MessageBusServices.class).addServiceBus(MediatorTransport::configure);

        ServiceProvider serviceProvider = services.buildServiceProvider();

        assertSame(custom, serviceProvider.getRequiredService(RecurringJobProvider.class));
        assertTrue(serviceProvider.getRequiredService(RecurringJobScheduler.class)
                instanceof RecurringJobSchedulerImpl);
    }

    @Test
    void fixedIntervalMisfiresMatchSharedCrossLanguageFixtures() throws Exception {
        List<FixedIntervalFixture> fixtures;
        try (InputStream stream = getClass().getResourceAsStream(
                "/scheduling/v1/fixed-interval-misfires.json")) {
            if (stream == null) {
                throw new IllegalStateException("Scheduling fixtures were not found");
            }
            fixtures = new ObjectMapper().findAndRegisterModules().readValue(
                    stream,
                    new TypeReference<List<FixedIntervalFixture>>() { });
        }

        for (FixedIntervalFixture fixture : fixtures) {
            RecordingJobClient publisher = new RecordingJobClient();
            ManualDelayScheduler delays = new ManualDelayScheduler();
            MutableClock clock = new MutableClock(Instant.parse("2026-09-01T00:00:00Z"));
            InMemoryRecurringJobProvider provider = new InMemoryRecurringJobProvider(
                    publisher,
                    delays,
                    clock);
            provider.addOrUpdate(
                    new RecurringJobDefinition(
                            new RecurringJobIdentity(fixture.name()),
                            new FixedIntervalRecurringJobCadence(
                                    Duration.ofHours(1),
                                    Instant.parse("2026-09-01T00:00:00Z")),
                            null, null, null,
                            RecurringJobMisfirePolicy.valueOf(toEnumName(fixture.policy())),
                            fixture.maxCatchUpOccurrences(),
                            RecurringJobOverlapPolicy.ALLOW),
                    new TestJob(fixture.name())).toCompletableFuture().join();

            clock.setInstant(fixture.nowUtc());
            delays.runNext().toCompletableFuture().join();

            assertEquals(fixture.expectedDispatchCount(), publisher.messages.size(), fixture.name());
            assertEquals(fixture.expectedNextUtc(), delays.nextScheduledTime(), fixture.name());
        }
    }

    private static String toEnumName(String value) {
        return value.replaceAll("([a-z])([A-Z])", "$1_$2").toUpperCase();
    }

    private static InMemoryRecurringJobProvider createProvider(
            RecordingJobClient publisher,
            ManualDelayScheduler delays) {
        return new InMemoryRecurringJobProvider(
                publisher,
                delays,
                Clock.fixed(Instant.parse("2026-09-01T00:00:00Z"), ZoneOffset.UTC));
    }
}
