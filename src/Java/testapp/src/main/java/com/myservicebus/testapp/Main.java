package com.myservicebus.testapp;

import io.javalin.Javalin;
import java.io.IOException;
import java.util.UUID;
import java.util.concurrent.TimeoutException;
import java.util.concurrent.atomic.AtomicReference;

import com.myservicebus.ExceptionInfo;
import com.myservicebus.Fault;
import com.myservicebus.MyService;
import com.myservicebus.MyServiceImpl;
import com.myservicebus.RequestClientFactory;
import com.myservicebus.Response;
import com.myservicebus.SendEndpoint;
import com.myservicebus.ServiceBus;
import com.myservicebus.PublishEndpoint;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.rabbitmq.RabbitMqBusFactory;
import com.myservicebus.tasks.CancellationToken;

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
        });

        ServiceProvider provider = services.build();
        ServiceBus serviceBus = provider.getService(ServiceBus.class);

        try {
            serviceBus.start();
        } catch (IOException | TimeoutException e) {
            e.printStackTrace();
            return;
        }

        var app = Javalin.create().start(5301);

        app.get("/publish", ctx -> {
            try (ServiceScope scope = provider.createScope()) {

                var publishEndpoint = scope.getServiceProvider().getService(PublishEndpoint.class);
                SubmitOrder message = new SubmitOrder(UUID.randomUUID(), "MT Clone Java");
                try {
                    publishEndpoint.publish(message, CancellationToken.none).join();
                    ctx.result("Published SubmitOrder");
                } catch (Exception e) {
                    ctx.status(500).result("Failed to publish message");
                }
            }
        });

        app.get("/send", ctx -> {
            var sendEndpoint = provider.getService(SendEndpoint.class);
            SubmitOrder message = new SubmitOrder(UUID.randomUUID(), "MT Clone Java");
            try {
                sendEndpoint.send(message, CancellationToken.none).join();
                ctx.result("Sent SubmitOrder");
            } catch (Exception e) {
                ctx.status(500).result("Failed to send message");
            }
        });

        app.get("/request", ctx -> {
            var requestClientFactory = provider.getService(RequestClientFactory.class);
            var requestClient = requestClientFactory.create(TestRequest.class);
            try {
                var response = requestClient
                        .getResponse(new TestRequest("Foo"), TestResponse.class, CancellationToken.none)
                        .get();
                ctx.result(response.getMessage().toString());
            } catch (Exception exc) {
                ctx.result(exc.getMessage().toString());
            }
        });

        app.get("/request_multi", ctx -> {
            var requestClientFactory = provider.getService(RequestClientFactory.class);
            var requestClient = requestClientFactory.create(TestRequest.class);
            try {
                var response = requestClient
                        .getResponse(new TestRequest("Foo"), TestResponse.class,
                                Fault.class, CancellationToken.none)
                        .get();

                response.as(TestResponse.class).ifPresent((Response<TestResponse> r) -> {
                    ctx.result(r.getMessage().toString());
                });

                response.as(Fault.class).ifPresent(r -> {
                    var exception = (ExceptionInfo) r.getMessage().getExceptions().get(0);
                    String message = exception.getMessage();
                    if (message == null) {
                        message = exception.toString();
                    }
                    ctx.status(500).result(message);
                });
            } catch (Exception e) {
                ctx.status(500).result("Failed to get response: " + e.getMessage());
            }
        });

        System.out.println("Up and running");
    }
}
