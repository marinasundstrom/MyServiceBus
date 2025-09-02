package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;

class OrderSubmittedConsumer implements Consumer<OrderSubmitted> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<OrderSubmitted> context) throws Exception {
        var orderId = context
                .getMessage()
                .getOrderId();

        System.out.println("Order submitted: " + orderId);

        return CompletableFuture.completedFuture(null);
    }
}