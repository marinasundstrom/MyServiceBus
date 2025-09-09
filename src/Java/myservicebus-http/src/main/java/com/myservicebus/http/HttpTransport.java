package com.myservicebus.http;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.SendPipe;
import com.myservicebus.SendContextFactory;
import com.myservicebus.DefaultSendContextFactory;
import com.myservicebus.PublishContextFactory;
import com.myservicebus.DefaultPublishContextFactory;
import com.myservicebus.TransportSendEndpointProvider;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.logging.LoggerFactory;
import java.net.URI;

public final class HttpTransport {
    private HttpTransport() {
    }

    public static void configure(BusRegistrationConfigurator cfg, URI baseAddress) {
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
    }
}
