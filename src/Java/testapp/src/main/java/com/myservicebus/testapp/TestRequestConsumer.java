package com.myservicebus.testapp;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;

class TestRequestConsumer implements Consumer<TestRequest> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<TestRequest> context) throws Exception {
        var message = context
                .getMessage();

        System.out.println("Request: " + message);

        var response = new TestResponse(message + " 42");

        return context.respond(response, context.getCancellationToken());
    }
}