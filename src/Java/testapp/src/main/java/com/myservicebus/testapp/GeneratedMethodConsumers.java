package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;

import com.myservicebus.ConsumeContext;
import com.myservicebus.MessageConsumer;
import com.myservicebus.tasks.CancellationToken;

public final class GeneratedMethodConsumers {
    private GeneratedMethodConsumers() {
    }

    @MessageConsumer("generated-java-method")
    public static CompletionStage<Void> receiveGeneratedDispatch(
            GeneratedDispatchMessage request,
            ConsumeContext<GeneratedDispatchMessage> context,
            GeneratedDispatchProbe probe,
            CancellationToken cancellationToken) {
        probe.record(request, context, cancellationToken);
        return CompletableFuture.completedFuture(null);
    }

    @MessageConsumer
    public static void orderSubmittedConsumer(GeneratedConventionMessage message) {
    }
}
