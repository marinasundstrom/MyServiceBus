package com.myservicebus.http;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusImpl;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import java.util.function.BiConsumer;
import java.util.function.Consumer;

public final class HttpMessageBusFactory {
    private HttpMessageBusFactory() {
    }

    public static void configure(ServiceCollection services,
            Consumer<BusRegistrationConfigurator> configureBus,
            BiConsumer<BusRegistrationContext, HttpFactoryConfigurator> configure) {
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(services);
        if (configureBus != null) {
            configureBus.accept(cfg);
        }
        HttpTransport.configure(cfg);
        cfg.complete();
        services.addSingleton(MessageBus.class, sp -> () -> {
            HttpFactoryConfigurator factoryConfigurator = sp.getService(HttpFactoryConfigurator.class);
            if (configure != null) {
                BusRegistrationContext context = new BusRegistrationContext(sp);
                configure.accept(context, factoryConfigurator);
            }
            return new MessageBusImpl(sp);
        });
    }

    public static void configure(ServiceCollection services,
            Consumer<BusRegistrationConfigurator> configureBus) {
        configure(services, configureBus, null);
    }
}
