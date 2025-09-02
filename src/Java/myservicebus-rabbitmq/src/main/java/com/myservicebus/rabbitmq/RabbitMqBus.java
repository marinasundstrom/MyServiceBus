package com.myservicebus.rabbitmq;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.ServiceBus;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import java.io.IOException;
import java.util.concurrent.TimeoutException;
import java.util.function.BiConsumer;
import java.util.function.Consumer;

public class RabbitMqBus {
    private final ServiceBus bus;

    private RabbitMqBus(ServiceBus bus) {
        this.bus = bus;
    }

    public static RabbitMqBus configure(ServiceCollection services,
            Consumer<BusRegistrationConfigurator> configureBus,
            BiConsumer<BusRegistrationContext, RabbitMqFactoryConfigurator> configure) {
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(services);
        if (configureBus != null) {
            configureBus.accept(cfg);
        }
        RabbitMqTransport.configure(cfg);
        cfg.complete();
        ServiceProvider provider = services.build();
        if (configure != null) {
            BusRegistrationContext context = new BusRegistrationContext(provider);
            RabbitMqFactoryConfigurator factoryConfigurator = provider.getService(RabbitMqFactoryConfigurator.class);
            configure.accept(context, factoryConfigurator);
        }
        ServiceBus serviceBus = new ServiceBus(provider);
        return new RabbitMqBus(serviceBus);
    }

    public static RabbitMqBus configure(ServiceCollection services,
            Consumer<BusRegistrationConfigurator> configureBus) {
        return configure(services, configureBus, null);
    }

    public void start() throws IOException, TimeoutException {
        bus.start();
    }

    public void publish(Object message) throws IOException {
        bus.publish(message);
    }

    public void stop() throws IOException, TimeoutException {
        bus.stop();
    }
}

