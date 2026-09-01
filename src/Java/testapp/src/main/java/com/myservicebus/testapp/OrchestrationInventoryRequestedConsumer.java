package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;

class OrchestrationInventoryRequestedConsumer implements Consumer<OrchestrationInventoryRequested> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<OrchestrationInventoryRequested> context) {
        return context.publish(
                new OrchestrationInventoryReserved(context.getMessage().getOrderId()),
                context.getCancellationToken());
    }
}
