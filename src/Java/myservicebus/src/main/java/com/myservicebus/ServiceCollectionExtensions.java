package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import java.util.function.Consumer;

public class ServiceCollectionExtensions {
    public static ServiceCollection addServiceBus(ServiceCollection thiz,
            Consumer<BusRegistrationConfigurator> configure) {
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(thiz);
        if (configure != null) {
            configure.accept(cfg);
        }
        cfg.complete();
        return thiz;
    }
}
