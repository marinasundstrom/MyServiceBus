package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.tasks.CancellationToken;

class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) throws Exception {
        var orderId = context
                .getMessage()
                .getOrderId();

        return context.publish(new OrderSubmitted(orderId), CancellationToken.none);
    }
}