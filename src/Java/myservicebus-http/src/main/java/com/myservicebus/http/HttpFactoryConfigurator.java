package com.myservicebus.http;

import java.net.URI;
import java.util.ArrayList;
import java.util.List;
import java.util.function.BiConsumer;
import java.util.function.Consumer;

import com.myservicebus.BusFactoryConfigurator;
import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.BusRegistrationContext;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusImpl;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.topology.TopologyRegistry;

public class HttpFactoryConfigurator implements BusFactoryConfigurator {
    private URI baseAddress = URI.create("http://localhost/");
    private final List<Class<?>> consumers = new ArrayList<>();
    private final List<BiConsumer<BusRegistrationContext, HttpFactoryConfigurator>> endpointActions = new ArrayList<>();

    public void host(URI address) {
        this.baseAddress = address;
    }

    public URI getBaseAddress() {
        return baseAddress;
    }

    public void addConsumer(Class<?> consumerClass) {
        consumers.add(consumerClass);
    }

    public void receiveEndpoint(String path, Consumer<HttpReceiveEndpointConfigurator> configure) {
        if (configure != null) {
            endpointActions.add((context, cfg) -> {
                TopologyRegistry topology = context.getServiceProvider().getService(TopologyRegistry.class);
                HttpReceiveEndpointConfigurator endpointConfigurator =
                        new HttpReceiveEndpointConfigurator(topology, baseAddress, path);
                configure.accept(endpointConfigurator);
            });
        }
    }

    @Override
    public MessageBus build() {
        ServiceCollection services = new ServiceCollection();
        configure(services);
        ServiceProvider provider = services.buildServiceProvider();
        return provider.getService(MessageBus.class);
    }

    @Override
    public void configure(ServiceCollection services) {
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(services);
        for (Class<?> consumer : consumers) {
            cfg.addConsumer(consumer);
        }
        HttpTransport.configure(cfg, this);
        cfg.complete();
        services.addSingleton(MessageBus.class, sp -> () -> {
            BusRegistrationContext context = new BusRegistrationContext(sp);
            for (BiConsumer<BusRegistrationContext, HttpFactoryConfigurator> action : endpointActions) {
                action.accept(context, this);
            }
            return new MessageBusImpl(sp);
        });
    }
}
