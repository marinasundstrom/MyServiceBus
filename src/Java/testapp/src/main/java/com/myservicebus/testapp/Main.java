package com.myservicebus.testapp;

import io.javalin.Javalin;
import java.io.IOException;
import java.util.UUID;
import java.util.concurrent.TimeoutException;
import java.util.concurrent.atomic.AtomicReference;

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
                        .getResponse(new TestRequest("Foo"), TestResponse.class,
                                Fault.class, CancellationToken.none)
                        .get();

                var r1 = response.as(TestResponse.class);
                var r2 = response.as(Fault.class);

                if (r1.isPresent()) {
                    var message1 = r1.get().getMessage();
                    ctx.result(message1.getMessage());
                } else if (r2.isPresent()) {
                    var exception = r2.get().getMessage().getExceptions().get(0);
                    ctx.result(exception.toString());
                }
            } catch (Exception e) {
                ctx.status(500).result("Failed to get response: " + e.getMessage());
            }
        });

        System.out.println("Up and running");
    }
}
