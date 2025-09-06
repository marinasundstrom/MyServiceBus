package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.google.inject.Inject;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import org.slf4j.Logger;

class OrderSubmittedConsumer implements Consumer<OrderSubmitted> {
    private final Logger logger;

    @Inject
    public OrderSubmittedConsumer(Logger logger) {
        this.logger = logger;
    }

    @Override
    public CompletableFuture<Void> consume(ConsumeContext<OrderSubmitted> context) throws Exception {
        var message = context.getMessage();
        var orderId = message.getOrderId();
        var replica = message.getReplica();

        logger.info("📨 Order submitted: {} by {} ✅", orderId, replica);

        return CompletableFuture.completedFuture(null);
    }
}
