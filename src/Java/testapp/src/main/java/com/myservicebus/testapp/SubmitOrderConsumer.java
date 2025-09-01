package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.google.inject.Inject;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.MyService;

class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    private MyService service;

    @Inject
    public SubmitOrderConsumer(MyService service) {
        this.service = service;
    }

    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) throws Exception {
        var m = context
                .getMessage();

        var orderId = m.getOrderId();
        var message = m.getMessage();

        service.doWork();

        System.out.println("Order id: " + orderId + " (from " + message + ")");

        return context.publish(new OrderSubmitted(orderId), context.getCancellationToken());
    }
}