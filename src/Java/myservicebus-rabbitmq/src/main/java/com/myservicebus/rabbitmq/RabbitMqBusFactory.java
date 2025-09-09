package com.myservicebus.rabbitmq;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.BusRegistrationContext;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusImpl;
import com.myservicebus.ReceiveEndpointConnector;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import java.util.function.BiConsumer;
import java.util.function.Consumer;

/**
 * Configures RabbitMQ transport for the service bus without building the
 * service provider.
 * After calling {@link #configure(ServiceCollection, Consumer, BiConsumer)},
 * resolve
 * {@link MessageBus} from the built {@link ServiceProvider} and start
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
        services.addSingleton(MessageBus.class, sp -> () -> {
            RabbitMqFactoryConfigurator factoryConfigurator = sp.getService(RabbitMqFactoryConfigurator.class);
            if (configure != null) {
                BusRegistrationContext context = new BusRegistrationContext(sp);
                configure.accept(context, factoryConfigurator);
            }
            MessageBusImpl bus = new MessageBusImpl(sp);
            try {
                factoryConfigurator.applyHandlers(bus);
            } catch (Exception ex) {
                throw new RuntimeException("Failed to apply handlers", ex);
            }
            return bus;
        });
        // services.addSingleton(ReceiveEndpointConnector.class, sp -> () ->
        // sp.getService(MessageBus.class));
    }

    public static void configure(ServiceCollection services,
            Consumer<BusRegistrationConfigurator> configureBus) {
        configure(services, configureBus, null);
    }
}
