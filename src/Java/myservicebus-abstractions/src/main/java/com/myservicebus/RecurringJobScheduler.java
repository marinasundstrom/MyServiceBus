package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;
import java.util.concurrent.CompletionStage;

/**
 * Creates and controls recurring job definitions. Provider acceptance does not imply that an
 * occurrence's application work has completed.
 */
public interface RecurringJobScheduler {
    <TJob> CompletionStage<RecurringJobDefinitionReceipt> addOrUpdate(
            RecurringJobDefinition definition,
            TJob job,
            Long expectedRevision,
            CancellationToken cancellationToken);

    default <TJob> CompletionStage<RecurringJobDefinitionReceipt> addOrUpdate(
            RecurringJobDefinition definition,
            TJob job) {
        return addOrUpdate(definition, job, null, CancellationToken.none());
    }

    default <TJob> CompletionStage<RecurringJobDefinitionReceipt> addOrUpdate(
            RecurringJobDefinition definition,
            TJob job,
            Long expectedRevision) {
        return addOrUpdate(definition, job, expectedRevision, CancellationToken.none());
    }

    CompletionStage<RecurringJobControlResult> pause(
            RecurringJobIdentity identity,
            Long expectedRevision,
            CancellationToken cancellationToken);

    default CompletionStage<RecurringJobControlResult> pause(RecurringJobIdentity identity) {
        return pause(identity, null, CancellationToken.none());
    }

    default CompletionStage<RecurringJobControlResult> pause(
            RecurringJobIdentity identity,
            Long expectedRevision) {
        return pause(identity, expectedRevision, CancellationToken.none());
    }

    CompletionStage<RecurringJobControlResult> resume(
            RecurringJobIdentity identity,
            Long expectedRevision,
            CancellationToken cancellationToken);

    default CompletionStage<RecurringJobControlResult> resume(RecurringJobIdentity identity) {
        return resume(identity, null, CancellationToken.none());
    }

    default CompletionStage<RecurringJobControlResult> resume(
            RecurringJobIdentity identity,
            Long expectedRevision) {
        return resume(identity, expectedRevision, CancellationToken.none());
    }

    CompletionStage<RecurringJobControlResult> remove(
            RecurringJobIdentity identity,
            Long expectedRevision,
            CancellationToken cancellationToken);

    default CompletionStage<RecurringJobControlResult> remove(RecurringJobIdentity identity) {
        return remove(identity, null, CancellationToken.none());
    }

    default CompletionStage<RecurringJobControlResult> remove(
            RecurringJobIdentity identity,
            Long expectedRevision) {
        return remove(identity, expectedRevision, CancellationToken.none());
    }

    CompletionStage<RecurringJobOccurrenceReceipt> triggerNow(
            RecurringJobIdentity identity,
            CancellationToken cancellationToken);

    default CompletionStage<RecurringJobOccurrenceReceipt> triggerNow(RecurringJobIdentity identity) {
        return triggerNow(identity, CancellationToken.none());
    }
}
