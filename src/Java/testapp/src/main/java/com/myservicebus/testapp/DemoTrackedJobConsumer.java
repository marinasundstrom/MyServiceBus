package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import java.util.concurrent.TimeUnit;

import com.myservicebus.JobConsumer;
import com.myservicebus.JobContext;

public final class DemoTrackedJobConsumer implements JobConsumer<DemoTrackedJob> {
    @Override
    public CompletionStage<Void> run(JobContext<DemoTrackedJob> context) {
        if (context.getJob().failAlways()
                || context.getJob().failFirstAttempt() && context.getRetryAttempt() == 0) {
            return CompletableFuture.failedFuture(
                    new IllegalStateException("The sample report job was asked to demonstrate a failed attempt."));
        }

        CompletionStage<Void> progress = CompletableFuture.completedFuture(null);
        for (int step = 1; step <= 3; step++) {
            int value = step;
            progress = progress
                    .thenCompose(ignored -> context.setProgress(value, 3L))
                    .thenRunAsync(
                            () -> {
                            },
                            CompletableFuture.delayedExecutor(250, TimeUnit.MILLISECONDS));
        }
        return progress;
    }
}
