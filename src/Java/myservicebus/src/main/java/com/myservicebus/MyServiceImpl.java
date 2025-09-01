package com.myservicebus;

import com.google.inject.Inject;
import com.myservicebus.di.ServiceProvider;

public class MyServiceImpl implements MyService {
    private ServiceProvider serviceProvider;

    @Inject
    public MyServiceImpl(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;

    }

    public void doWork() {
        var secondService = serviceProvider.getService(MySecondService.class);
        secondService.doSomething();
    }
}
