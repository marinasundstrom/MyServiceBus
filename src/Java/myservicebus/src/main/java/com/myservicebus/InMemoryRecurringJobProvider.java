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

public final class InMemoryRecurringJobProvider implements RecurringJobProvider, RecurringJobSource {
    private static final class Entry {
        private final UUID definitionId;
        private RecurringJobDefinition definition;
        private Object job;
        private Submission submit;
        private long revision;
        private Instant acceptedAtUtc;
        private RecurringJobDefinitionStatus status;
        private Instant nextOccurrenceAtUtc;
        private UUID timerToken;

        private Entry(
                UUID definitionId,
                RecurringJobDefinition definition,
                Object job,
                Submission submit,
                long revision,
                Instant acceptedAtUtc) {
            this.definitionId = definitionId;
            this.definition = definition;
            this.job = job;
            this.submit = submit;
            this.revision = revision;
            this.acceptedAtUtc = acceptedAtUtc;
            this.status = RecurringJobDefinitionStatus.ACTIVE;
        }
    }

    private record OccurrenceKey(UUID definitionId, long revision, Instant scheduledForUtc) {
    }

    @FunctionalInterface
    private interface Submission {
        CompletionStage<Void> apply(UUID occurrenceId, CancellationToken cancellationToken);
    }

    private final Object gate = new Object();
    private final Map<RecurringJobIdentity, Entry> definitions = new HashMap<>();
    private final Set<OccurrenceKey> occurrences = new HashSet<>();
    private final JobClient jobClient;
    private final LocalDelayScheduler delayScheduler;
    private final Clock clock;

    public InMemoryRecurringJobProvider(
            JobClient jobClient,
            LocalDelayScheduler delayScheduler) {
        this(jobClient, delayScheduler, Clock.systemUTC());
    }

    public InMemoryRecurringJobProvider(
            JobClient jobClient,
            LocalDelayScheduler delayScheduler,
            Clock clock) {
        this.jobClient = jobClient;
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
    public String getProvider() {
        return getProviderName();
    }

    @Override
    public boolean isAuthoritative() {
        return true;
    }

    @Override
    public CompletionStage<java.util.List<RecurringJobState>> getSnapshot(int maximumCount) {
        if (maximumCount <= 0) {
            throw new IllegalArgumentException("maximumCount must be greater than zero");
        }
        synchronized (gate) {
            return CompletableFuture.completedFuture(definitions.values().stream()
                    .filter(entry -> entry.status != RecurringJobDefinitionStatus.REMOVED)
                    .sorted(java.util.Comparator.comparing(
                            entry -> entry.nextOccurrenceAtUtc,
                            java.util.Comparator.nullsLast(java.util.Comparator.naturalOrder())))
                    .limit(maximumCount)
                    .map(entry -> new RecurringJobState(
                            entry.definitionId,
                            entry.definition.identity(),
                            entry.revision,
                            getProviderName(),
                            getDurability(),
                            getPlacement(),
                            formatCadence(entry.definition.cadence()),
                            entry.job.getClass().getName(),
                            entry.status,
                            entry.nextOccurrenceAtUtc,
                            entry.acceptedAtUtc))
                    .toList());
        }
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
                    (occurrenceId, token) -> jobClient.submit(
                            job,
                            new JobSubmissionOptions(null, occurrenceId),
                            token)
                            .thenApply(ignored -> null),
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
        return entry.submit.apply(occurrenceId, cancellationToken).thenApply(ignored ->
                new RecurringJobOccurrenceReceipt(
                        occurrenceId,
                        captured.definitionId,
                        captured.revision,
                        scheduledFor,
                        true,
                        RecurringJobOccurrenceStatus.PENDING));
    }

    private CompletionStage<Void> scheduleNext(
            Entry entry,
            Instant afterUtc,
            CancellationToken cancellationToken) {
        Instant next = calculateNext(entry, afterUtc);
        return scheduleAt(entry, next, cancellationToken);
    }

    private CompletionStage<Void> scheduleAt(
            Entry entry,
            Instant next,
            CancellationToken cancellationToken) {
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
        int dispatchCount;
        Instant next;
        synchronized (gate) {
            entry = definitions.get(identity);
            if (entry == null
                    || entry.revision != revision
                    || entry.status != RecurringJobDefinitionStatus.ACTIVE
                    || occurrences.contains(new OccurrenceKey(entry.definitionId, revision, scheduledForUtc))) {
                return CompletableFuture.completedFuture(null);
            }
            entry.timerToken = null;
            entry.nextOccurrenceAtUtc = null;
            Evaluation evaluation = evaluateDue(entry, scheduledForUtc, clock.instant());
            dispatchCount = evaluation.dispatchCount();
            next = evaluation.next();
            occurrences.add(new OccurrenceKey(entry.definitionId, revision, scheduledForUtc));
            Duration interval = ((FixedIntervalRecurringJobCadence) entry.definition.cadence()).interval();
            for (int index = 1; index < dispatchCount; index++) {
                occurrences.add(new OccurrenceKey(
                        entry.definitionId,
                        revision,
                        scheduledForUtc.plus(interval.multipliedBy(index))));
            }
        }

        Entry captured = entry;
        CompletionStage<Void> dispatches = scheduleAt(entry, next, cancellationToken);
        for (int index = 0; index < dispatchCount; index++) {
            dispatches = dispatches.thenCompose(ignored ->
                    captured.submit.apply(UUID.randomUUID(), cancellationToken));
        }
        return dispatches;
    }

    private boolean isCurrent(Entry entry) {
        return definitions.get(entry.definition.identity()) == entry;
    }

    private record Evaluation(int dispatchCount, Instant next) {
    }

    private static Evaluation evaluateDue(Entry entry, Instant scheduledForUtc, Instant nowUtc) {
        Instant following = calculateNext(entry, scheduledForUtc);
        boolean misfire = following != null && !following.isAfter(nowUtc);
        if (!misfire) {
            return new Evaluation(1, following);
        }

        int dispatchCount = switch (entry.definition.misfirePolicy()) {
            case SKIP -> 0;
            case FIRE_ONCE_NOW -> 1;
            case CATCH_UP -> countCatchUpOccurrences(entry, scheduledForUtc, nowUtc);
        };
        return new Evaluation(dispatchCount, calculateNext(entry, nowUtc));
    }

    private static int countCatchUpOccurrences(Entry entry, Instant scheduledForUtc, Instant nowUtc) {
        Duration interval = ((FixedIntervalRecurringJobCadence) entry.definition.cadence()).interval();
        Instant lastEligible = entry.definition.endAtUtc() != null
                && !entry.definition.endAtUtc().isAfter(nowUtc)
                ? entry.definition.endAtUtc().minusNanos(1)
                : nowUtc;
        long elapsedIntervals = Duration.between(scheduledForUtc, lastEligible).dividedBy(interval);
        return (int) Math.min(Math.addExact(elapsedIntervals, 1), entry.definition.maxCatchUpOccurrences());
    }

    private static Instant calculateNext(Entry entry, Instant afterUtc) {
        RecurringJobDefinition definition = entry.definition;
        FixedIntervalRecurringJobCadence cadence =
                (FixedIntervalRecurringJobCadence) definition.cadence();
        Instant anchor = cadence.anchorAtUtc() != null
                ? cadence.anchorAtUtc()
                : definition.startAtUtc() != null ? definition.startAtUtc() : entry.acceptedAtUtc;
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

    private static String formatCadence(RecurringJobCadence cadence) {
        if (cadence instanceof FixedIntervalRecurringJobCadence fixedInterval) {
            return "Every " + fixedInterval.interval();
        }
        if (cadence instanceof CronRecurringJobCadence cron) {
            return cron.dialect() + ": " + cron.expression() + " (" + cron.timeZoneId() + ")";
        }
        return cadence.getClass().getSimpleName();
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
