package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutionException;

import com.google.inject.Inject;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;

class SubmitOrderErrorConsumer implements Consumer<SubmitOrder> {
    private final Logger logger;

    @Inject
    public SubmitOrderErrorConsumer(LoggerFactory loggerFactory) {
        this.logger = loggerFactory.create(SubmitOrderErrorConsumer.class);
    }

    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) throws Exception {
        var msg = context.getMessage();
        System.out.println(msg.getOrderId());
        // inspect, fix, or forward the failed message

        // context.forward("queue:submit-order", msg).get();
        // logger.info("➡️ Forwarded error message. Order id: " + msg.getOrderId());

        return CompletableFuture.completedFuture(null);
    }
}
