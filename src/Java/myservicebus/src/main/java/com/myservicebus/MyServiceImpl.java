package com.myservicebus;

import java.util.Random;

import com.myservicebus.di.ServiceProvider;
import javax.inject.Inject;

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
