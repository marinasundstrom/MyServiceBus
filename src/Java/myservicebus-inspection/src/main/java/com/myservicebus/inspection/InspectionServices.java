package com.myservicebus.inspection;

import com.myservicebus.MessageBus;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceCollectionDecorator;

public class InspectionServices extends ServiceCollectionDecorator {
    public InspectionServices(ServiceCollection inner) {
        super(inner);
    }

    public ServiceCollection addInspection() {
        inner.addSingleton(BusInspectionProvider.class,
                sp -> () -> new DefaultBusInspectionProvider(sp.getService(MessageBus.class)));
        return inner;
    }
}
