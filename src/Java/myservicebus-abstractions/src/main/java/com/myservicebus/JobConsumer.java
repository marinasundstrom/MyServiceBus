package com.myservicebus;

import java.util.concurrent.CompletionStage;

@FunctionalInterface
public interface JobConsumer<TJob> {
    CompletionStage<Void> run(JobContext<TJob> context) throws Exception;
}

