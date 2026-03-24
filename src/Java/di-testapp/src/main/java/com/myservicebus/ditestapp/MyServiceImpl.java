package com.myservicebus.ditestapp;

import com.myservicebus.di.ServiceProvider;
import javax.inject.Inject;

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
