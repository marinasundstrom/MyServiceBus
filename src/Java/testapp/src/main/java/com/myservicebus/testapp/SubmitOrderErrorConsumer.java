package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutionException;

import com.google.inject.Inject;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.Fault;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;

class SubmitOrderErrorConsumer implements Consumer<Fault<SubmitOrder>> {
    private final Logger logger;

    @Inject
    public SubmitOrderErrorConsumer(LoggerFactory loggerFactory) {
        this.logger = loggerFactory.create(SubmitOrderConsumer.class);
    }

    @Override
    public CompletableFuture<Void> consume(ConsumeContext<Fault<SubmitOrder>> context) throws Exception {
        var msg = context.getMessage().getMessage();
        System.out.println(msg.getOrderId());
        // inspect, fix, or forward the failed message
        try {
            context.forward("queue:submit-order", msg).get();
            System.out.println("➡️ Forwarded error message. Order id: " + msg.getOrderId());
        } catch (InterruptedException | ExecutionException e1) {

        }
        return CompletableFuture.completedFuture(null);
    }
}
