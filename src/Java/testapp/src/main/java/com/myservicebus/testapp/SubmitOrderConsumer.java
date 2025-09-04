package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.google.inject.Inject;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.MyService;
import org.slf4j.Logger;

class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    private final MyService service;
    private final Logger logger;

    @Inject
    public SubmitOrderConsumer(MyService service, Logger logger) {
        this.service = service;
        this.logger = logger;
    }

    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) throws Exception {
        var m = context.getMessage();

        var orderId = m.getOrderId();
        var message = m.getMessage();

        service.doWork();

        logger.info("📨 Order id: {} (from {}) ✅", orderId, message);

        return context.publish(new OrderSubmitted(orderId), context.getCancellationToken());
    }
}
