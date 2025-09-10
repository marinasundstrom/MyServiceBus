package com.myservicebus;

import java.lang.reflect.Method;
import java.util.function.Consumer;

import com.myservicebus.ScopeConsumerFactory;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceCollectionDecorator;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.ConsoleLoggerFactory;
import com.myservicebus.logging.ConsoleLoggerConfig;

public class MessageBusServices extends ServiceCollectionDecorator {

    public MessageBusServices(ServiceCollection inner) {
        super(inner);
    }

    public ServiceCollection addServiceBus(Consumer<BusRegistrationConfigurator> configure) {

        boolean hasLogger = inner.getDescriptors().stream()
                .anyMatch(d -> d.getServiceType().equals(LoggerFactory.class));
        if (!hasLogger) {
            inner.addSingleton(LoggerFactory.class,
                    sp -> () -> new ConsoleLoggerFactory(new ConsoleLoggerConfig()));
        }

        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(inner);
        if (configure != null) {
            configure.accept(cfg);
        }
        cfg.complete();

        inner.addSingleton(MessageBus.class, sp -> () -> {
            Object factoryConfigurator = null;
            if (cfg.getFactoryConfiguratorClass() != null) {
                factoryConfigurator = sp.getService(cfg.getFactoryConfiguratorClass());
                if (cfg.getTransportConfigure() != null) {
                    BusRegistrationContext context = new BusRegistrationContext(sp);
                    cfg.getTransportConfigure().accept(context, factoryConfigurator);
                }
            }
            MessageBusImpl bus = new MessageBusImpl(sp, type -> new ScopeConsumerFactory(sp));
            if (factoryConfigurator != null) {
                try {
                    Method m = factoryConfigurator.getClass().getDeclaredMethod("applyHandlers", MessageBusImpl.class);
                    m.setAccessible(true);
                    m.invoke(factoryConfigurator, bus);
                } catch (ReflectiveOperationException ex) {
                    throw new RuntimeException("Failed to apply handlers", ex);
                }
            }
            return bus;
        });

        inner.addSingleton(ReceiveEndpointConnector.class,
                sp -> () -> (ReceiveEndpointConnector) sp.getService(MessageBus.class));

        inner.addSingleton(JobScheduler.class, sp -> () -> new DefaultJobScheduler());
        inner.addScoped(MessageScheduler.class,
                sp -> () -> new MessageSchedulerImpl(
                        (PublishEndpoint) sp.getService(MessageBus.class),
                        (SendEndpointProvider) sp.getService(MessageBus.class),
                        (JobScheduler) sp.getService(JobScheduler.class)));

        return inner;
    }
}
