package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import javax.naming.OperationNotSupportedException;

import com.google.inject.Inject;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import org.slf4j.Logger;

class TestRequestConsumer implements Consumer<TestRequest> {
    private final Logger logger;

    @Inject
    public TestRequestConsumer(Logger logger) {
        this.logger = logger;
    }

    @Override
    public CompletableFuture<Void> consume(ConsumeContext<TestRequest> context) throws Exception {
        var message = context.getMessage();

        logger.info("Request: {}", message);

        var response = new TestResponse(message + " 42");

        throw new OperationNotSupportedException();

        // return context.respond(response, context.getCancellationToken());
    }
}