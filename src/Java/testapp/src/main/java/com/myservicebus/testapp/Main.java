package com.myservicebus.testapp;

import io.javalin.Javalin;
import java.io.IOException;
import java.util.UUID;
import java.util.concurrent.TimeoutException;

import com.myservicebus.MyService;
import com.myservicebus.MyServiceImpl;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.rabbitmq.RabbitMqBus;

public class Main {
    public static void main(String[] args) {
        ServiceCollection services = new ServiceCollection();
        services.addScoped(MyService.class, MyServiceImpl.class);

        RabbitMqBus serviceBus = RabbitMqBus.configure(services, cfg -> {
            cfg.addConsumer(SubmitOrderConsumer.class);
        }, (context, cfg) -> {
            cfg.host("localhost", h -> {
                h.username("guest");
                h.password("guest");
            });
        });

        try {
            serviceBus.start();
        } catch (IOException | TimeoutException e) {
            e.printStackTrace();
            return;
        }

        var app = Javalin.create().start(5301);

        app.get("/publish", ctx -> {
            SubmitOrder message = new SubmitOrder(UUID.randomUUID(), "MT Clone Java");
            try {
                serviceBus.publish(message);
                ctx.result("Published SubmitOrder");
            } catch (IOException e) {
                ctx.status(500).result("Failed to publish message");
            }
        });

        System.out.println("Up and running");
    }

    public static void main_old(String[] args) throws Exception {
        ServiceCollection services = new ServiceCollection();
        services.addScoped(MyService.class, MyServiceImpl.class);

        var serviceBus = RabbitMqBus.configure(services, cfg -> {
            cfg.addConsumer(SubmitOrderConsumer.class);
        }, (context, cfg) -> {
            cfg.host("localhost", h -> {
                h.username("guest");
                h.password("guest");
            });

            // cfg.message(SubmitOrder.class, m -> {
            // m.setEntityName("TestApp.SubmitOrder");
            // });

            // cfg.message(OrderSubmitted.class, m -> {
            // m.setEntityName("TestApp.OrderSubmitted");
            // });

            // cfg.receiveEndpoint("submit-order-consumer", e -> {
            // e.configureConsumer(context, SubmitOrderConsumer.class);
            // });
        });

        serviceBus.start();

        System.out.println("Up and running");

        SubmitOrder message = new SubmitOrder(UUID.randomUUID(), "MT Clone Java");

        serviceBus.publish(message);

        System.out.println("Waiting");

        System.in.read();
    }
}
