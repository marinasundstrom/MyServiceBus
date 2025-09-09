package com.myservicebus;

import com.myservicebus.di.ServiceCollection;

public interface BusFactoryConfigurator {
    MessageBus build();

    void build(ServiceCollection services);
}
