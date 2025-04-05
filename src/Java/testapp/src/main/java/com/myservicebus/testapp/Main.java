package com.myservicebus.testapp;

import com.myservicebus.MyService;
import com.myservicebus.MyServiceImpl;
import com.myservicebus.ServiceBus;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.rabbitmq.RabbitMqBusRegistrationConfiguratorExtensions;

public class Main {
    public static void main(String[] args) throws Exception {
        System.out.println("Hello world!");

        /*
         * ServiceCollection services = new ServiceCollection();
         * services.addScoped(MyService.class, MyServiceImpl.class);
         * services.addScoped(SubmitOrderConsumer.class);
         * 
         * ServiceProvider provider = services.build();
         * 
         * try (ServiceScope scope = provider.createScope()) {
         * SubmitOrderConsumer consumer = scope.getService(SubmitOrderConsumer.class);
         * 
         * var consumeContext = new ConsumeContext<SubmitOrder>(SubmitOrder.class);
         * 
         * consumer.consume(consumeContext)
         * .thenRun(() -> System.out.println("completed"))
         * .join();
         * }
         */

        ServiceCollection services = new ServiceCollection();
        services.addScoped(MyService.class, MyServiceImpl.class);

        var serviceBus = ServiceBus.configure(services, x -> {
            x.addConsumer(SubmitOrderConsumer.class);

            RabbitMqBusRegistrationConfiguratorExtensions.usingRabbitMq(x, (context, cfg) -> {
                cfg.host("rabbitmq://localhost");

                cfg.receiveEndpoint("submit-order-queue", e -> {
                    e.configureConsumer(context, SubmitOrderConsumer.class);
                });
            });
        });

        serviceBus.start();
    }
}