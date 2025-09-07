package com.myservicebus.rabbitmq;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.RequestClientFactory;
import com.myservicebus.ScopedClientFactory;
import com.myservicebus.RequestClientTransport;
import com.myservicebus.SendPipe;
import com.myservicebus.SendContextFactory;
import com.myservicebus.PublishContextFactory;
import com.myservicebus.TransportSendEndpointProvider;
import com.myservicebus.di.ServiceCollection;
import com.rabbitmq.client.ConnectionFactory;
import java.net.URI;

public class RabbitMqTransport {

    // Equivalent to "UsingRabbitMq" in .NET impl
    public static void configure(BusRegistrationConfigurator x) {

        RabbitMqFactoryConfigurator factoryConfigurator = new RabbitMqFactoryConfigurator();

        ServiceCollection services = x.getServiceCollection();
        services.addSingleton(RabbitMqFactoryConfigurator.class, sp -> () -> factoryConfigurator);
        services.addSingleton(ConnectionProvider.class, sp -> () -> {
            ConnectionFactory factory = new ConnectionFactory();
            factory.setHost(factoryConfigurator.getClientHost());
            factory.setUsername(factoryConfigurator.getUsername());
            factory.setPassword(factoryConfigurator.getPassword());
            return new ConnectionProvider(factory);
        });
        services.addSingleton(URI.class, sp -> () -> URI.create("rabbitmq://" + factoryConfigurator.getClientHost() + "/"));
        services.addSingleton(RabbitMqTransportFactory.class, sp -> () -> {
            ConnectionProvider provider = sp.getService(ConnectionProvider.class);
            RabbitMqFactoryConfigurator cfgRef = sp.getService(RabbitMqFactoryConfigurator.class);
            return new RabbitMqTransportFactory(provider, cfgRef);
        });

        services.addSingleton(com.myservicebus.TransportFactory.class,
                sp -> () -> sp.getService(RabbitMqTransportFactory.class));

        services.addSingleton(SendContextFactory.class, sp -> () -> new RabbitMqSendContextFactory());
        services.addSingleton(PublishContextFactory.class, sp -> () -> new RabbitMqPublishContextFactory());

        services.addSingleton(RabbitMqSendEndpointProvider.class, sp -> () -> {
            RabbitMqTransportFactory factory = sp.getService(RabbitMqTransportFactory.class);
            SendPipe sendPipe = sp.getService(SendPipe.class);
            com.myservicebus.serialization.MessageSerializer serializer = sp.getService(com.myservicebus.serialization.MessageSerializer.class);
            URI busAddress = sp.getService(URI.class);
            SendContextFactory sendContextFactory = sp.getService(SendContextFactory.class);
            return new RabbitMqSendEndpointProvider(factory, sendPipe, serializer, busAddress, sendContextFactory);
        });
        services.addSingleton(TransportSendEndpointProvider.class,
                sp -> () -> sp.getService(RabbitMqSendEndpointProvider.class));
        services.addSingleton(RequestClientTransport.class,
                sp -> () -> new RabbitMqRequestClientTransport(sp.getService(ConnectionProvider.class)));
        services.addScoped(ScopedClientFactory.class,
                sp -> () -> new RequestClientFactory(sp.getService(RequestClientTransport.class)));
    }
}
