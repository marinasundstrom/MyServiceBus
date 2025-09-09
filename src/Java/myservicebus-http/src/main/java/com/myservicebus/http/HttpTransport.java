package com.myservicebus.http;

import java.net.URI;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.DefaultPublishContextFactory;
import com.myservicebus.DefaultSendContextFactory;
import com.myservicebus.PublishContextFactory;
import com.myservicebus.SendContextFactory;
import com.myservicebus.SendPipe;
import com.myservicebus.TransportSendEndpointProvider;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.serialization.MessageSerializer;

public final class HttpTransport {
    private HttpTransport() {
    }

    public static void configure(BusRegistrationConfigurator cfg, HttpFactoryConfigurator factoryConfigurator) {
        ServiceCollection services = cfg.getServiceCollection();
        services.addSingleton(HttpFactoryConfigurator.class, sp -> () -> factoryConfigurator);
        services.addSingleton(HttpTransportFactory.class, sp -> () -> new HttpTransportFactory());
        services.addSingleton(com.myservicebus.TransportFactory.class,
                sp -> () -> sp.getService(HttpTransportFactory.class));
        services.addSingleton(URI.class, sp -> () -> factoryConfigurator.getBaseAddress());
        services.addSingleton(SendContextFactory.class, sp -> () -> new DefaultSendContextFactory());
        services.addSingleton(PublishContextFactory.class, sp -> () -> new DefaultPublishContextFactory());
        services.addSingleton(HttpSendEndpointProvider.class, sp -> () -> {
            HttpTransportFactory factory = sp.getService(HttpTransportFactory.class);
            SendPipe sendPipe = sp.getService(SendPipe.class);
            MessageSerializer serializer = sp.getService(MessageSerializer.class);
            SendContextFactory sendContextFactory = sp.getService(SendContextFactory.class);
            LoggerFactory loggerFactory = sp.getService(LoggerFactory.class);
            URI baseAddress = factoryConfigurator.getBaseAddress();
            return new HttpSendEndpointProvider(factory, sendPipe, serializer, baseAddress, sendContextFactory,
                    loggerFactory);
        });
        services.addSingleton(TransportSendEndpointProvider.class,
                sp -> () -> sp.getService(HttpSendEndpointProvider.class));
    }

    public static void configure(BusRegistrationConfigurator cfg) {
        configure(cfg, new HttpFactoryConfigurator(cfg));
    }
}
