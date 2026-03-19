package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.google.inject.Inject;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;

class TestRequestConsumer implements Consumer<TestRequest> {
    private final Logger logger;

    @Inject
    public TestRequestConsumer(LoggerFactory loggerFactory) {
        this.logger = loggerFactory.create(TestRequestConsumer.class);
    }

    @Override
    public CompletableFuture<Void> consume(ConsumeContext<TestRequest> context) throws Exception {
        var message = context.getMessage().getMessage();

        logger.info("📨 Request: {}", message);

        if (DemoScenario.shouldFaultRequest(message)) {
            logger.warn("⚠️ TestRequest marked as fault case");
            throw new IllegalStateException("TestRequest demo fault");
        }

        var response = new TestResponse(message + " 42");
        return context.respond(response, context.getCancellationToken());
    }
}
