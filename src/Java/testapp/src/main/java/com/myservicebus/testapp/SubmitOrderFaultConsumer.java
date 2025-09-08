package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.google.inject.Inject;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.Fault;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;

class SubmitOrderFaultConsumer implements Consumer<Fault<OrderSubmitted>> {
    private final Logger logger;

    @Inject
    public SubmitOrderFaultConsumer(LoggerFactory loggerFactory) {
        this.logger = loggerFactory.create(SubmitOrderFaultConsumer.class);
    }

    @Override
    public CompletableFuture<Void> consume(ConsumeContext<Fault<OrderSubmitted>> context) throws Exception {
        var fault = context.getMessage();
        var msg = fault.getMessage();
        String error = fault.getExceptions().isEmpty() ? "" : fault.getExceptions().get(0).getMessage();
        logger.warn("⚠️ OrderSubmitted fault for {}: {}", msg.getOrderId(), error);
        return CompletableFuture.completedFuture(null);
    }
}
