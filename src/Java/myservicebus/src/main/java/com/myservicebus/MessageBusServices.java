package com.myservicebus;

import java.lang.reflect.Method;
import java.util.function.Consumer;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceCollectionDecorator;

public class MessageBusServices extends ServiceCollectionDecorator {

    public MessageBusServices(ServiceCollection inner) {
        super(inner);
    }

    public <T extends BusFactoryConfigurator> ServiceCollection addServiceBus(
            Class<T> factoryType,
            Consumer<BusRegistrationConfigurator> configure) {

        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(inner);
        T factoryConfigurator;
        try {
            factoryConfigurator = factoryType.getDeclaredConstructor().newInstance();
        } catch (ReflectiveOperationException ex) {
            throw new RuntimeException("Failed to create factory configurator", ex);
        }

        BusRegistrationServices services = new BusRegistrationServices(cfg, factoryConfigurator);
        if (configure != null) {
            configure.accept(services);
        }
        cfg.complete();

        inner.addSingleton(MessageBus.class, sp -> () -> {
            if (services.getTransportConfigure() != null) {
                BusRegistrationContext context = new BusRegistrationContext(sp);
                services.getTransportConfigure().accept(context, services.getFactoryConfigurator());
            }
            MessageBusImpl bus = new MessageBusImpl(sp);
            try {
                Method m = services.getFactoryConfigurator().getClass().getDeclaredMethod("applyHandlers",
                        MessageBusImpl.class);
                m.setAccessible(true);
                m.invoke(services.getFactoryConfigurator(), bus);
            } catch (ReflectiveOperationException ex) {
                throw new RuntimeException("Failed to apply handlers", ex);
            }
            return bus;
        });

        inner.addSingleton(ReceiveEndpointConnector.class,
                sp -> () -> (ReceiveEndpointConnector) sp.getService(MessageBus.class));

        return inner;
    }
}
