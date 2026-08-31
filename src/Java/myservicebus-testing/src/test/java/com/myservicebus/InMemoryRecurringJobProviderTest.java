package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.mediator.MediatorTransport;
import com.myservicebus.tasks.CancellationToken;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import java.util.function.Function;
import org.junit.jupiter.api.Test;

class InMemoryRecurringJobProviderTest {
    private record TestJob(String value) {
    }

    private static final class RecordingPublishEndpoint implements PublishEndpoint {
        private final List<Object> messages = new ArrayList<>();

        @Override
        public <T> CompletableFuture<Void> publish(T message, CancellationToken cancellationToken) {
            messages.add(message);
            return CompletableFuture.completedFuture(null);
        }
    }

    private static final class ManualDelayScheduler implements LocalDelayScheduler {
        private final Map<UUID, Function<CancellationToken, CompletionStage<Void>>> callbacks =
                new LinkedHashMap<>();

        @Override
        public CompletionStage<UUID> schedule(
                Instant scheduledTime,
                Function<CancellationToken, CompletionStage<Void>> callback,
                CancellationToken cancellationToken) {
            UUID token = UUID.randomUUID();
            callbacks.put(token, callback);
            return CompletableFuture.completedFuture(token);
        }

        @Override
        public CompletionStage<Boolean> cancel(UUID tokenId) {
            return CompletableFuture.completedFuture(callbacks.remove(tokenId) != null);
        }

        CompletionStage<Void> runNext() {
            Map.Entry<UUID, Function<CancellationToken, CompletionStage<Void>>> next =
                    callbacks.entrySet().iterator().next();
            callbacks.remove(next.getKey());
            return next.getValue().apply(CancellationToken.none());
        }
    }

    @Test
    void addOrUpdateIsIdempotentAndRevisionsChangedContent() {
        RecordingPublishEndpoint publisher = new RecordingPublishEndpoint();
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
        InMemoryRecurringJobProvider provider = createProvider(new RecordingPublishEndpoint(), delays);
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
    void dueAndManualOccurrencesDispatchTheJobCommand() {
        RecordingPublishEndpoint publisher = new RecordingPublishEndpoint();
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
        assertEquals(RecurringJobOccurrenceStatus.DISPATCHED, manual.status());
        assertEquals(2, publisher.messages.size());
        assertEquals(1, delays.callbacks.size());
    }

    @Test
    void unsupportedCadenceAndOverlapAreRejected() {
        InMemoryRecurringJobProvider provider = createProvider(
                new RecordingPublishEndpoint(),
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
                new RecordingPublishEndpoint(),
                new ManualDelayScheduler());
        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(RecurringJobProvider.class, () -> custom);
        services.from(MessageBusServices.class).addServiceBus(MediatorTransport::configure);

        ServiceProvider serviceProvider = services.buildServiceProvider();

        assertSame(custom, serviceProvider.getRequiredService(RecurringJobProvider.class));
        assertTrue(serviceProvider.getRequiredService(RecurringJobScheduler.class)
                instanceof RecurringJobSchedulerImpl);
    }

    private static InMemoryRecurringJobProvider createProvider(
            RecordingPublishEndpoint publisher,
            ManualDelayScheduler delays) {
        return new InMemoryRecurringJobProvider(
                publisher,
                delays,
                Clock.fixed(Instant.parse("2026-09-01T00:00:00Z"), ZoneOffset.UTC));
    }
}
