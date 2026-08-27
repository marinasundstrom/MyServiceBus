package com.myservicebus.azure.servicebus;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.DefaultPublishContextFactory;
import com.myservicebus.DefaultSendContextFactory;
import com.myservicebus.PublishContextFactory;
import com.myservicebus.RequestClientFactory;
import com.myservicebus.RequestClientTransport;
import com.myservicebus.SendContextFactory;
import com.myservicebus.SendPipe;
import com.myservicebus.ScopedClientFactory;
import com.myservicebus.TransportCapabilityDescriptor;
import com.myservicebus.TransportCapabilityDescriptors;
import com.myservicebus.TransportSendEndpointProvider;
import com.myservicebus.TransportRequestClientTransport;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.serialization.MessageSerializer;

import java.net.URI;

public final class AzureServiceBusTransport {
    private AzureServiceBusTransport() {
    }

    public static void configure(
            BusRegistrationConfigurator configurator,
            AzureServiceBusFactoryConfigurator factoryConfigurator) {
        ServiceCollection services = configurator.getServiceCollection();
        services.addSingleton(AzureServiceBusFactoryConfigurator.class, provider -> () -> factoryConfigurator);
        services.addSingleton(TransportCapabilityDescriptor.class,
                provider -> () -> TransportCapabilityDescriptors.AZURE_SERVICE_BUS);
        services.addSingleton(URI.class,
                provider -> () -> AzureServiceBusTransportFactory.endpoint(factoryConfigurator.getConnectionString()));
        services.addSingleton(AzureServiceBusTransportFactory.class, provider -> () ->
                new AzureServiceBusTransportFactory(
                        provider.getService(AzureServiceBusFactoryConfigurator.class),
                        provider.getService(LoggerFactory.class)));
        services.addSingleton(com.myservicebus.TransportFactory.class,
                provider -> () -> provider.getService(AzureServiceBusTransportFactory.class));
        services.addSingleton(SendContextFactory.class, provider -> () -> new DefaultSendContextFactory());
        services.addSingleton(PublishContextFactory.class, provider -> () -> new DefaultPublishContextFactory());
        services.addSingleton(AzureServiceBusSendEndpointProvider.class, provider -> () ->
                new AzureServiceBusSendEndpointProvider(
                        provider.getService(AzureServiceBusTransportFactory.class),
                        provider.getService(SendPipe.class),
                        provider.getService(MessageSerializer.class),
                        provider.getService(URI.class),
                        provider.getService(SendContextFactory.class)));
        services.addSingleton(TransportSendEndpointProvider.class,
                provider -> () -> provider.getService(AzureServiceBusSendEndpointProvider.class));
        services.addSingleton(RequestClientTransport.class, provider -> () ->
                new TransportRequestClientTransport(
                        provider.getService(com.myservicebus.TransportFactory.class),
                        provider.getService(MessageSerializer.class)));
        services.addScoped(ScopedClientFactory.class, provider -> () ->
                new RequestClientFactory(provider.getService(RequestClientTransport.class)));
    }

    public static void configure(BusRegistrationConfigurator configurator) {
        configure(configurator, new AzureServiceBusFactoryConfigurator());
    }
}
