package com.myservicebus.rabbitmq;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.di.ServiceCollection;
import com.rabbitmq.client.ConnectionFactory;
import java.util.function.BiConsumer;

public class RabbitMqTransport {

    // Equivalent to "UsingRabbitMq" in .NET impl
    public static void configure(BusRegistrationConfigurator x,
            BiConsumer<BusRegistrationContext, RabbitMqFactoryConfigurator> configure) {

        RabbitMqFactoryConfigurator factoryConfigurator = new RabbitMqFactoryConfigurator();
        if (configure != null) {
            configure.accept(null, factoryConfigurator);
        }

        ServiceCollection services = x.getServiceCollection();
        services.addSingleton(RabbitMqFactoryConfigurator.class, sp -> () -> factoryConfigurator);
        services.addSingleton(ConnectionProvider.class, sp -> () -> {
            ConnectionFactory factory = new ConnectionFactory();
            factory.setHost(factoryConfigurator.getClientHost());
            factory.setUsername(factoryConfigurator.getUsername());
            factory.setPassword(factoryConfigurator.getPassword());
            return new ConnectionProvider(factory);
        });
        services.addSingleton(RabbitMqTransportFactory.class, sp -> () -> {
            ConnectionProvider provider = sp.getService(ConnectionProvider.class);
            return new RabbitMqTransportFactory(provider);
        });

        services.addSingleton(RabbitMqSendEndpointProvider.class, sp -> () -> {
            RabbitMqTransportFactory factory = sp.getService(RabbitMqTransportFactory.class);
            return new RabbitMqSendEndpointProvider(factory);
        });

        services.addSingleton(SendEndpointProvider.class, sp -> () -> sp.getService(RabbitMqSendEndpointProvider.class));
    }
}
