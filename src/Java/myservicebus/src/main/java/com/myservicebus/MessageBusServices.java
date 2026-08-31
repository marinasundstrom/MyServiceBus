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

        inner.addSingleton(LocalDelayScheduler.class, sp -> () -> new DefaultLocalDelayScheduler());
        inner.tryAddSingleton(RecurringJobProvider.class,
                sp -> () -> new InMemoryRecurringJobProvider(
                        (PublishEndpoint) sp.getService(MessageBus.class),
                        (LocalDelayScheduler) sp.getService(LocalDelayScheduler.class)));
        inner.tryAddSingleton(RecurringJobSource.class,
                sp -> () -> (RecurringJobSource) sp.getRequiredService(RecurringJobProvider.class));
        inner.tryAddSingleton(RecurringJobScheduler.class,
                sp -> () -> new RecurringJobSchedulerImpl(
                        (RecurringJobProvider) sp.getService(RecurringJobProvider.class)));
        inner.tryAddSingleton(InMemoryScheduledWorkSource.class, sp -> () -> new InMemoryScheduledWorkSource());
        inner.tryAddSingleton(ScheduledWorkSource.class,
                sp -> () -> sp.getRequiredService(InMemoryScheduledWorkSource.class));
        inner.tryAddScoped(ScheduleMessageProvider.class,
                sp -> () -> new InMemoryScheduleMessageProvider(
                        (PublishEndpoint) sp.getService(MessageBus.class),
                        (SendEndpointProvider) sp.getService(MessageBus.class),
                        (LocalDelayScheduler) sp.getService(LocalDelayScheduler.class),
                        sp.getRequiredService(InMemoryScheduledWorkSource.class),
                        sp.getServices(ScheduledWorkObserver.class)));
        inner.addScoped(MessageScheduler.class,
                sp -> () -> new MessageSchedulerImpl(
                        (ScheduleMessageProvider) sp.getService(ScheduleMessageProvider.class)));

        return inner;
    }
}
