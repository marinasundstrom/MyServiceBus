package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;

class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    public CompletableFuture<Void> Consume(ConsumeContext<SubmitOrder> context) {
        var orderId = context
                .getMessage()
                .getOrderId();

        return context.publish(new OrderSubmitted(orderId));
    }
}