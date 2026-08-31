package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;
import java.util.concurrent.CompletionStage;

public final class RecurringJobSchedulerImpl implements RecurringJobScheduler {
    private final RecurringJobProvider provider;

    public RecurringJobSchedulerImpl(RecurringJobProvider provider) {
        this.provider = provider;
    }

    @Override
    public <TJob> CompletionStage<RecurringJobDefinitionReceipt> addOrUpdate(
            RecurringJobDefinition definition,
            TJob job,
            Long expectedRevision,
            CancellationToken cancellationToken) {
        return provider.addOrUpdate(definition, job, expectedRevision, cancellationToken);
    }

    @Override
    public CompletionStage<RecurringJobControlResult> pause(
            RecurringJobIdentity identity,
            Long expectedRevision,
            CancellationToken cancellationToken) {
        return provider.pause(identity, expectedRevision, cancellationToken);
    }

    @Override
    public CompletionStage<RecurringJobControlResult> resume(
            RecurringJobIdentity identity,
            Long expectedRevision,
            CancellationToken cancellationToken) {
        return provider.resume(identity, expectedRevision, cancellationToken);
    }

    @Override
    public CompletionStage<RecurringJobControlResult> remove(
            RecurringJobIdentity identity,
            Long expectedRevision,
            CancellationToken cancellationToken) {
        return provider.remove(identity, expectedRevision, cancellationToken);
    }

    @Override
    public CompletionStage<RecurringJobOccurrenceReceipt> triggerNow(
            RecurringJobIdentity identity,
            CancellationToken cancellationToken) {
        return provider.triggerNow(identity, cancellationToken);
    }
}
