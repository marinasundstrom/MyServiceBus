package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.google.inject.Inject;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.MyService;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;

class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    private final MyService service;
    private final Logger logger;

    @Inject
    public SubmitOrderConsumer(MyService service, LoggerFactory loggerFactory) {
        this.service = service;
        this.logger = loggerFactory.create(SubmitOrderConsumer.class);
    }

    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) throws Exception {
        var m = context.getMessage();

        var orderId = m.getOrderId();
        var message = m.getMessage();

        service.doWork();

        String replica = System.getenv().getOrDefault("HTTP_PORT", "unknown");
        logger.info("📨 Order id: {} (from {}) handled by {} ✅", orderId, message, replica);

        return context.publish(new OrderSubmitted(orderId, replica), context.getCancellationToken());
    }
}
