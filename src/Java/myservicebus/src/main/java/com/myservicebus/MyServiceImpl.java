package com.myservicebus;

import java.util.Random;

import com.google.inject.Inject;
import com.myservicebus.di.ServiceProvider;

public class MyServiceImpl implements MyService {
    private ServiceProvider serviceProvider;
    private static final Random random = new Random();

    @Inject
    public MyServiceImpl(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;

    }

    public void doWork() {
        var secondService = serviceProvider.getService(MySecondService.class);
        secondService.doSomething();

        if (random.nextBoolean()) { // 50% chance
            throw new RuntimeException("Something went wrong!");
        }
    }
}
