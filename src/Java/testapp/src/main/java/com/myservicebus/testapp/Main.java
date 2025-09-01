package com.myservicebus.testapp;

import java.util.UUID;

import com.myservicebus.MyService;
import com.myservicebus.MyServiceImpl;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.rabbitmq.RabbitMqBus;

public class Main {
    public static void main(String[] args) throws Exception {
        ServiceCollection services = new ServiceCollection();
        services.addScoped(MyService.class, MyServiceImpl.class);

        var serviceBus = RabbitMqBus.configure(services, cfg -> {
            cfg.addConsumer(SubmitOrderConsumer.class);

            cfg.host("localhost", h -> {
                h.username("guest");
                h.password("guest");
            });

            /*
             * cfg.message(SubmitOrder.class, m -> {
             * m.setEntityName("TestApp.SubmitOrder");
             * });
             * 
             * cfg.message(OrderSubmitted.class, m -> {
             * m.setEntityName("TestApp.OrderSubmitted");
             * });
             * 
             * cfg.receiveEndpoint("submit-order-consumer", e -> {
             * e.configureConsumer(context, SubmitOrderConsumer.class);
             * });
             */
        });

        serviceBus.start();

        System.out.println("Up and running");

        SubmitOrder message = new SubmitOrder(UUID.randomUUID(), "MT Clone Java");

        serviceBus.publish(message);

        System.out.println("Waiting");

        System.in.read();
    }
}