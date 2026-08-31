package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Objects;
import java.util.Set;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import java.util.function.Function;

public final class InMemoryRecurringJobProvider implements RecurringJobProvider {
    private static final class Entry {
        private final UUID definitionId;
        private RecurringJobDefinition definition;
        private Object job;
        private Function<CancellationToken, CompletionStage<Void>> dispatch;
        private long revision;
        private Instant acceptedAtUtc;
        private RecurringJobDefinitionStatus status;
        private Instant nextOccurrenceAtUtc;
        private UUID timerToken;

        private Entry(
                UUID definitionId,
                RecurringJobDefinition definition,
                Object job,
                Function<CancellationToken, CompletionStage<Void>> dispatch,
                long revision,
                Instant acceptedAtUtc) {
            this.definitionId = definitionId;
            this.definition = definition;
            this.job = job;
            this.dispatch = dispatch;
            this.revision = revision;
            this.acceptedAtUtc = acceptedAtUtc;
            this.status = RecurringJobDefinitionStatus.ACTIVE;
        }
    }

    private record OccurrenceKey(UUID definitionId, long revision, Instant scheduledForUtc) {
    }

    private final Object gate = new Object();
    private final Map<RecurringJobIdentity, Entry> definitions = new HashMap<>();
    private final Set<OccurrenceKey> occurrences = new HashSet<>();
    private final PublishEndpoint publishEndpoint;
    private final LocalDelayScheduler delayScheduler;
    private final Clock clock;

    public InMemoryRecurringJobProvider(
            PublishEndpoint publishEndpoint,
            LocalDelayScheduler delayScheduler) {
        this(publishEndpoint, delayScheduler, Clock.systemUTC());
    }

    public InMemoryRecurringJobProvider(
            PublishEndpoint publishEndpoint,
            LocalDelayScheduler delayScheduler,
            Clock clock) {
        this.publishEndpoint = publishEndpoint;
        this.delayScheduler = delayScheduler;
        this.clock = clock;
    }

    @Override
    public String getProviderName() {
        return "InMemory";
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
    public <TJob> CompletionStage<RecurringJobDefinitionReceipt> addOrUpdate(
            RecurringJobDefinition definition,
            TJob job,
            Long expectedRevision,
            CancellationToken cancellationToken) {
        Objects.requireNonNull(definition, "definition");
        Objects.requireNonNull(job, "job");
        ensureSupported(definition);

        Entry entry;
        UUID timerToCancel;
        synchronized (gate) {
            Entry current = definitions.get(definition.identity());
            validateExpectedRevision(definition.identity(), expectedRevision, current == null ? 0 : current.revision);
            if (current != null
                    && current.status != RecurringJobDefinitionStatus.REMOVED
                    && current.definition.equals(definition)
                    && Objects.equals(current.job, job)) {
                return CompletableFuture.completedFuture(createReceipt(current));
            }

            timerToCancel = current == null ? null : current.timerToken;
            entry = new Entry(
                    current == null ? UUID.randomUUID() : current.definitionId,
                    definition,
                    job,
                    token -> publishEndpoint.publish(job, token),
                    (current == null ? 0 : current.revision) + 1,
                    clock.instant());
            definitions.put(definition.identity(), entry);
        }

        CompletionStage<Boolean> cancelled = timerToCancel == null
                ? CompletableFuture.completedFuture(false)
                : delayScheduler.cancel(timerToCancel);
        return cancelled.thenCompose(ignored -> scheduleNext(entry, clock.instant(), cancellationToken))
                .thenApply(ignored -> createReceipt(entry));
    }

    @Override
    public CompletionStage<RecurringJobControlResult> pause(
            RecurringJobIdentity identity,
            Long expectedRevision,
            CancellationToken cancellationToken) {
        UUID timer;
        long revision;
        synchronized (gate) {
            Entry entry = definitions.get(identity);
            if (entry == null || entry.status == RecurringJobDefinitionStatus.REMOVED) {
                return CompletableFuture.completedFuture(
                        new RecurringJobControlResult(RecurringJobControlOutcome.NOT_FOUND));
            }
            validateExpectedRevision(identity, expectedRevision, entry.revision);
            if (entry.status == RecurringJobDefinitionStatus.PAUSED) {
                return CompletableFuture.completedFuture(
                        new RecurringJobControlResult(RecurringJobControlOutcome.UNCHANGED, entry.revision));
            }
            entry.status = RecurringJobDefinitionStatus.PAUSED;
            entry.revision++;
            entry.nextOccurrenceAtUtc = null;
            timer = entry.timerToken;
            entry.timerToken = null;
            revision = entry.revision;
        }

        CompletionStage<Boolean> cancelled = timer == null
                ? CompletableFuture.completedFuture(false)
                : delayScheduler.cancel(timer);
        return cancelled.thenApply(ignored ->
                new RecurringJobControlResult(RecurringJobControlOutcome.APPLIED, revision));
    }

    @Override
    public CompletionStage<RecurringJobControlResult> resume(
            RecurringJobIdentity identity,
            Long expectedRevision,
            CancellationToken cancellationToken) {
        Entry entry;
        synchronized (gate) {
            entry = definitions.get(identity);
            if (entry == null || entry.status == RecurringJobDefinitionStatus.REMOVED) {
                return CompletableFuture.completedFuture(
                        new RecurringJobControlResult(RecurringJobControlOutcome.NOT_FOUND));
            }
            validateExpectedRevision(identity, expectedRevision, entry.revision);
            if (entry.status == RecurringJobDefinitionStatus.ACTIVE) {
                return CompletableFuture.completedFuture(
                        new RecurringJobControlResult(RecurringJobControlOutcome.UNCHANGED, entry.revision));
            }
            entry.status = RecurringJobDefinitionStatus.ACTIVE;
            entry.revision++;
        }

        return scheduleNext(entry, clock.instant(), cancellationToken).thenApply(ignored ->
                new RecurringJobControlResult(RecurringJobControlOutcome.APPLIED, entry.revision));
    }

    @Override
    public CompletionStage<RecurringJobControlResult> remove(
            RecurringJobIdentity identity,
            Long expectedRevision,
            CancellationToken cancellationToken) {
        UUID timer;
        long revision;
        synchronized (gate) {
            Entry entry = definitions.get(identity);
            if (entry == null || entry.status == RecurringJobDefinitionStatus.REMOVED) {
                return CompletableFuture.completedFuture(
                        new RecurringJobControlResult(RecurringJobControlOutcome.NOT_FOUND));
            }
            validateExpectedRevision(identity, expectedRevision, entry.revision);
            entry.status = RecurringJobDefinitionStatus.REMOVED;
            entry.revision++;
            entry.nextOccurrenceAtUtc = null;
            timer = entry.timerToken;
            entry.timerToken = null;
            revision = entry.revision;
        }

        CompletionStage<Boolean> cancelled = timer == null
                ? CompletableFuture.completedFuture(false)
                : delayScheduler.cancel(timer);
        return cancelled.thenApply(ignored ->
                new RecurringJobControlResult(RecurringJobControlOutcome.APPLIED, revision));
    }

    @Override
    public CompletionStage<RecurringJobOccurrenceReceipt> triggerNow(
            RecurringJobIdentity identity,
            CancellationToken cancellationToken) {
        Entry entry;
        Instant scheduledFor;
        UUID occurrenceId = UUID.randomUUID();
        synchronized (gate) {
            entry = definitions.get(identity);
            if (entry == null
                    || entry.status == RecurringJobDefinitionStatus.REMOVED
                    || entry.status == RecurringJobDefinitionStatus.ENDED) {
                throw new RecurringJobNotFoundException(identity);
            }
            scheduledFor = clock.instant();
        }

        Entry captured = entry;
        return entry.dispatch.apply(cancellationToken).thenApply(ignored ->
                new RecurringJobOccurrenceReceipt(
                        occurrenceId,
                        captured.definitionId,
                        captured.revision,
                        scheduledFor,
                        true,
                        RecurringJobOccurrenceStatus.DISPATCHED));
    }

    private CompletionStage<Void> scheduleNext(
            Entry entry,
            Instant afterUtc,
            CancellationToken cancellationToken) {
        Instant next = calculateNext(entry.definition, afterUtc);
        if (next == null) {
            synchronized (gate) {
                if (isCurrent(entry)) {
                    entry.status = RecurringJobDefinitionStatus.ENDED;
                    entry.nextOccurrenceAtUtc = null;
                }
            }
            return CompletableFuture.completedFuture(null);
        }

        RecurringJobIdentity identity = entry.definition.identity();
        long revision = entry.revision;
        return delayScheduler.schedule(
                next,
                token -> materialize(identity, revision, next, token),
                cancellationToken).thenCompose(timerToken -> {
                    boolean cancelTimer;
                    synchronized (gate) {
                        cancelTimer = !isCurrent(entry)
                                || entry.status != RecurringJobDefinitionStatus.ACTIVE;
                        if (!cancelTimer) {
                            entry.nextOccurrenceAtUtc = next;
                            entry.timerToken = timerToken;
                        }
                    }
                    if (cancelTimer) {
                        return delayScheduler.cancel(timerToken).thenApply(ignored -> null);
                    }
                    return CompletableFuture.completedFuture(null);
                });
    }

    private CompletionStage<Void> materialize(
            RecurringJobIdentity identity,
            long revision,
            Instant scheduledForUtc,
            CancellationToken cancellationToken) {
        Entry entry;
        synchronized (gate) {
            entry = definitions.get(identity);
            if (entry == null
                    || entry.revision != revision
                    || entry.status != RecurringJobDefinitionStatus.ACTIVE
                    || !occurrences.add(new OccurrenceKey(entry.definitionId, revision, scheduledForUtc))) {
                return CompletableFuture.completedFuture(null);
            }
            entry.timerToken = null;
            entry.nextOccurrenceAtUtc = null;
        }

        Entry captured = entry;
        return scheduleNext(entry, scheduledForUtc, cancellationToken)
                .thenCompose(ignored -> captured.dispatch.apply(cancellationToken));
    }

    private boolean isCurrent(Entry entry) {
        return definitions.get(entry.definition.identity()) == entry;
    }

    private static Instant calculateNext(RecurringJobDefinition definition, Instant afterUtc) {
        FixedIntervalRecurringJobCadence cadence =
                (FixedIntervalRecurringJobCadence) definition.cadence();
        Instant anchor = cadence.anchorAtUtc() != null
                ? cadence.anchorAtUtc()
                : definition.startAtUtc() != null ? definition.startAtUtc() : afterUtc;
        Instant threshold = definition.startAtUtc() != null && definition.startAtUtc().isAfter(afterUtc)
                ? definition.startAtUtc().minusNanos(1)
                : afterUtc;
        Instant next;
        if (anchor.isAfter(threshold)) {
            next = anchor;
        } else {
            Duration elapsed = Duration.between(anchor, threshold);
            long steps = elapsed.dividedBy(cadence.interval()) + 1;
            next = anchor.plus(cadence.interval().multipliedBy(steps));
        }
        return definition.endAtUtc() != null && !next.isBefore(definition.endAtUtc()) ? null : next;
    }

    private static void ensureSupported(RecurringJobDefinition definition) {
        if (!(definition.cadence() instanceof FixedIntervalRecurringJobCadence)) {
            throw new UnsupportedOperationException(
                    "The in-memory recurring scheduler currently supports fixed intervals only.");
        }
        if (definition.overlapPolicy() != RecurringJobOverlapPolicy.ALLOW) {
            throw new UnsupportedOperationException(
                    "The dispatch-only recurring scheduler supports the ALLOW overlap policy only.");
        }
    }

    private static void validateExpectedRevision(
            RecurringJobIdentity identity,
            Long expectedRevision,
            long currentRevision) {
        if (expectedRevision != null && expectedRevision != currentRevision) {
            throw new RecurringJobRevisionConflictException(identity, expectedRevision, currentRevision);
        }
    }

    private RecurringJobDefinitionReceipt createReceipt(Entry entry) {
        return new RecurringJobDefinitionReceipt(
                entry.definitionId,
                entry.definition.identity(),
                entry.revision,
                getProviderName(),
                getDurability(),
                getPlacement(),
                entry.acceptedAtUtc,
                entry.nextOccurrenceAtUtc);
    }
}
