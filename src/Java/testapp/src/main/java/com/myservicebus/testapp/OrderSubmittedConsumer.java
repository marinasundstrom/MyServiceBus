package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.google.inject.Inject;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;

class OrderSubmittedConsumer implements Consumer<OrderSubmitted> {
    private final Logger logger;

    @Inject
    public OrderSubmittedConsumer(LoggerFactory loggerFactory) {
        this.logger = loggerFactory.create(OrderSubmittedConsumer.class);
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
