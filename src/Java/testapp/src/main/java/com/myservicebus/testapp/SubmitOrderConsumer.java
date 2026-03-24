package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import javax.inject.Inject;

class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    private final Logger logger;

    @Inject
    public SubmitOrderConsumer(LoggerFactory loggerFactory) {
        this.logger = loggerFactory.create(SubmitOrderConsumer.class);
    }

    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) throws Exception {
        var m = context.getMessage();

        var orderId = m.getOrderId();
        var message = m.getMessage();

        if (DemoScenario.shouldFaultSubmit(message)) {
            logger.warn("⚠️ SubmitOrder marked as fault case");
            throw new IllegalStateException("SubmitOrder demo fault");
        }

        String replica = System.getenv().getOrDefault("HTTP_PORT", "unknown");
        logger.info("📨 Order id: {} (from {}) handled by {} ✅", orderId, message, replica);

        return context.publish(new OrderSubmitted(orderId, replica), context.getCancellationToken());
    }
}
