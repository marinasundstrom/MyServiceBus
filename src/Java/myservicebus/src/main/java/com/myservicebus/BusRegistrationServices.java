package com.myservicebus;

import java.lang.reflect.Method;
import java.util.function.BiConsumer;

public class BusRegistrationServices extends BusRegistrationConfiguratorDecorator {

    private final BusFactoryConfigurator factoryConfigurator;
    private BiConsumer<BusRegistrationContext, BusFactoryConfigurator> transportConfigure;

    public BusRegistrationServices(BusRegistrationConfigurator inner, BusFactoryConfigurator factoryConfigurator) {
        super(inner);
        this.factoryConfigurator = factoryConfigurator;
    }

    @SuppressWarnings({ "unchecked", "rawtypes" })
    public <TConfigurator> BusRegistrationConfigurator using(Class<?> transportClass,
            BiConsumer<BusRegistrationContext, TConfigurator> configure) {
        try {
            Method m = transportClass.getDeclaredMethod("configure", BusRegistrationConfigurator.class,
                    factoryConfigurator.getClass());
            m.setAccessible(true);
            m.invoke(null, inner, factoryConfigurator);
        } catch (ReflectiveOperationException ex) {
            throw new RuntimeException("Failed to configure transport", ex);
        }

        if (configure != null) {
            this.transportConfigure = (BiConsumer) configure;
        }

        return this;
    }

    BiConsumer<BusRegistrationContext, BusFactoryConfigurator> getTransportConfigure() {
        return transportConfigure;
    }

    BusFactoryConfigurator getFactoryConfigurator() {
        return factoryConfigurator;
    }
}

