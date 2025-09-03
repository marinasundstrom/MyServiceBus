package com.myservicebus.rabbitmq;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.RabbitMqMessageBus;
import com.myservicebus.SendEndpoint;
import com.myservicebus.PublishEndpoint;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import java.util.function.BiConsumer;
import java.util.function.Consumer;

/**
 * Configures RabbitMQ transport for the service bus without building the
 * service provider.
 * After calling {@link #configure(ServiceCollection, Consumer, BiConsumer)},
 * resolve
 * {@link RabbitMqMessageBus} from the built {@link ServiceProvider} and start
 * it manually.
 */
public final class RabbitMqBusFactory {
    private RabbitMqBusFactory() {
    }

    public static void configure(ServiceCollection services,
            Consumer<BusRegistrationConfigurator> configureBus,
            BiConsumer<BusRegistrationContext, RabbitMqFactoryConfigurator> configure) {
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(services);
        if (configureBus != null) {
            configureBus.accept(cfg);
        }
        RabbitMqTransport.configure(cfg);
        cfg.complete();
        services.addSingleton(RabbitMqMessageBus.class, sp -> () -> {
            if (configure != null) {
                BusRegistrationContext context = new BusRegistrationContext(sp);
                RabbitMqFactoryConfigurator factoryConfigurator = sp.getService(RabbitMqFactoryConfigurator.class);
                configure.accept(context, factoryConfigurator);
            }
            return new RabbitMqMessageBus(sp);
        });
        services.addSingleton(SendEndpoint.class, sp -> () -> sp.getService(RabbitMqMessageBus.class));
        services.addScoped(PublishEndpoint.class, sp -> () -> sp.getService(RabbitMqMessageBus.class));
    }

    public static void configure(ServiceCollection services,
            Consumer<BusRegistrationConfigurator> configureBus) {
        configure(services, configureBus, null);
    }
}
