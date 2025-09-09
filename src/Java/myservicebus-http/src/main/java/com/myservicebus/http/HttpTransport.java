package com.myservicebus.http;

import java.net.URI;
import java.util.function.Consumer;

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
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.TopologyRegistry;

public final class HttpTransport {
    private HttpTransport() {
    }

    public static void configure(BusRegistrationConfigurator cfg, URI baseAddress) {
        configure(cfg, baseAddress, null);
    }

    public static void configure(BusRegistrationConfigurator cfg, URI baseAddress,
            Consumer<HttpFactoryConfigurator> configure) {
        ServiceCollection services = cfg.getServiceCollection();
        services.addSingleton(HttpTransportFactory.class, sp -> () -> new HttpTransportFactory());
        services.addSingleton(com.myservicebus.TransportFactory.class,
                sp -> () -> sp.getService(HttpTransportFactory.class));
        services.addSingleton(URI.class, sp -> () -> baseAddress);
        services.addSingleton(SendContextFactory.class, sp -> () -> new DefaultSendContextFactory());
        services.addSingleton(PublishContextFactory.class, sp -> () -> new DefaultPublishContextFactory());
        services.addSingleton(HttpSendEndpointProvider.class, sp -> () -> {
            HttpTransportFactory factory = sp.getService(HttpTransportFactory.class);
            SendPipe sendPipe = sp.getService(SendPipe.class);
            MessageSerializer serializer = sp.getService(MessageSerializer.class);
            SendContextFactory sendContextFactory = sp.getService(SendContextFactory.class);
            LoggerFactory loggerFactory = sp.getService(LoggerFactory.class);
            return new HttpSendEndpointProvider(factory, sendPipe, serializer, baseAddress, sendContextFactory,
                    loggerFactory);
        });
        services.addSingleton(TransportSendEndpointProvider.class,
                sp -> () -> sp.getService(HttpSendEndpointProvider.class));

        TopologyRegistry registry = cfg.getTopologyRegistry();
        for (ConsumerTopology consumer : registry.getConsumers()) {
            String addr = consumer.getAddress();
            if (addr != null && !addr.contains("://")) {
                URI uri = baseAddress.resolve(addr);
                consumer.setAddress(uri.toString());
            }
        }

        if (configure != null) {
            HttpFactoryConfigurator factory = new HttpFactoryConfigurator(cfg, baseAddress);
            configure.accept(factory);
        }
    }
}
