package com.myservicebus.testapp;

import java.util.UUID;

import com.myservicebus.MyService;
import com.myservicebus.MyServiceImpl;
import com.myservicebus.ServiceBus;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.rabbitmq.RabbitMqTransport;

public class Main {
    public static void main(String[] args) throws Exception {
        ServiceCollection services = new ServiceCollection();
        services.addScoped(MyService.class, MyServiceImpl.class);

        var serviceBus = ServiceBus.configure(services, x -> {
            x.addConsumer(SubmitOrderConsumer.class);

            RabbitMqTransport.configure(x, (context, cfg) -> {
                cfg.host("rabbitmq://localhost", h -> {
                    h.username("guest");
                    h.password("guest");
                });

                cfg.message(SubmitOrder.class, m -> {
                    m.setEntityName("TestApp.SubmitOrder");
                });

                cfg.message(OrderSubmitted.class, m -> {
                    m.setEntityName("TestApp.OrderSubmitted");
                });

                cfg.receiveEndpoint("submit-order-consumer", e -> {
                    e.configureConsumer(context, SubmitOrderConsumer.class);
                });
            });
        });

        serviceBus.start();

        System.out.println("Up and running");

        SubmitOrder message = new SubmitOrder(UUID.randomUUID());

        serviceBus.publish(message);

        System.out.println("Waiting");

        System.in.read();
    }
}