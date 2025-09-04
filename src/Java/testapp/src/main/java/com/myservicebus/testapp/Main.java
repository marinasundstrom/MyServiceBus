package com.myservicebus.testapp;

import io.javalin.Javalin;
import java.io.IOException;
import java.util.UUID;
import java.util.concurrent.TimeoutException;

import com.myservicebus.ExceptionInfo;
import com.myservicebus.Fault;
import com.myservicebus.MyService;
import com.myservicebus.MyServiceImpl;
import com.myservicebus.RequestClientFactory;
import com.myservicebus.Response;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.MessageBus;
import com.myservicebus.PublishEndpoint;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.rabbitmq.RabbitMqBusFactory;
import com.myservicebus.tasks.CancellationToken;
import org.slf4j.Logger;

public class Main {
    public static void main(String[] args) {
        ServiceCollection services = new ServiceCollection();
        services.addScoped(MyService.class, MyServiceImpl.class);

        RabbitMqBusFactory.configure(services, cfg -> {
            cfg.addConsumer(SubmitOrderConsumer.class);
            cfg.addConsumer(OrderSubmittedConsumer.class);
            cfg.addConsumer(TestRequestConsumer.class);
        }, (context, cfg) -> {
            cfg.host("localhost", h -> {
                h.username("guest");
                h.password("guest");
            });

            cfg.configureEndpoints(context);
        });

        ServiceProvider provider = services.buildServiceProvider();
        final Logger logger = provider.getService(Logger.class);
        MessageBus serviceBus = provider.getService(MessageBus.class);

        try {
            serviceBus.start();
            logger.info("🚀 Test app started");
        } catch (Exception e) {
            logger.error("❌ Failed to start service bus", e);
            return;
        }

        var app = Javalin.create().start(5301);

        app.get("/publish", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var publishEndpoint = scope.getServiceProvider().getService(PublishEndpoint.class);
                SubmitOrder message = new SubmitOrder(UUID.randomUUID(), "MT Clone Java");
                try {
                    publishEndpoint.publish(message, CancellationToken.none).join();
                    logger.info("📤 Published SubmitOrder {} ✅", message.getOrderId());
                    ctx.result("Published SubmitOrder");
                } catch (Exception e) {
                    logger.error("❌ Failed to publish message", e);
                    ctx.status(500).result("Failed to publish message");
                }
            }
        });

        app.get("/send", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var scopedSp = scope.getServiceProvider();
                var sendEndpointProvider = scopedSp.getService(SendEndpointProvider.class);
                var sendEndpoint = sendEndpointProvider.getSendEndpoint("rabbitmq://localhost/submit-order-queue");
                SubmitOrder message = new SubmitOrder(UUID.randomUUID(), "MT Clone Java");
                try {
                    sendEndpoint.send(message, CancellationToken.none).join();
                    logger.info("📤 Sent SubmitOrder {} ✅", message.getOrderId());
                    ctx.result("Sent SubmitOrder");
                } catch (Exception e) {
                    logger.error("❌ Failed to send message", e);
                    ctx.status(500).result("Failed to send message");
                }
            }
        });

        app.get("/request", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var scopedSp = scope.getServiceProvider();
                var requestClientFactory = scopedSp.getService(RequestClientFactory.class);
                var requestClient = requestClientFactory.create(TestRequest.class);
                try {
                    var response = requestClient
                            .getResponse(new TestRequest("Foo"), TestResponse.class, CancellationToken.none)
                            .get();
                    logger.info("📨 Received response {} ✅", response.getMessage().toString());
                    ctx.result(response.getMessage().toString());
                } catch (Exception exc) {
                    logger.error("❌ Failed to get response", exc);
                    ctx.result(exc.getMessage().toString());
                }
            }
        });

        app.get("/request_multi", ctx -> {
            try (ServiceScope scope = provider.createScope()) {
                var scopedSp = scope.getServiceProvider();
                var requestClientFactory = scopedSp.getService(RequestClientFactory.class);
                var requestClient = requestClientFactory.create(TestRequest.class);
                try {
                    var response = requestClient
                            .getResponse(new TestRequest("Foo"), TestResponse.class,
                                    Fault.class, CancellationToken.none)
                            .get();

                    response.as(TestResponse.class).ifPresent((Response<TestResponse> r) -> {
                        logger.info("📨 Received response {} ✅", r.getMessage().toString());
                        ctx.result(r.getMessage().toString());
                    });

                    response.as(Fault.class).ifPresent(r -> {
                        var exception = (ExceptionInfo) r.getMessage().getExceptions().get(0);
                        String message = exception.getMessage();
                        if (message == null) {
                            message = exception.toString();
                        }
                        logger.error("❌ Fault received: {}", message);
                        ctx.status(500).result(message);
                    });
                } catch (Exception e) {
                    logger.error("❌ Failed to get response", e);
                    ctx.status(500).result("Failed to get response: " + e.getMessage());
                }
            }
        });

        logger.info("✅ Up and running");
    }
}
