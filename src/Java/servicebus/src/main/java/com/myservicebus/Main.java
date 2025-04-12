package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;

public class Main {
    public static void main(String[] args) {
        System.out.println("test");

        ServiceCollection services = new ServiceCollection();
        services.addSingleton(MyService.class, (sp) -> () -> new MyServiceImpl(sp));
        services.addScoped(MyScopedService.class);
        services.addScoped(MySecondService.class);

        // services.addServiceBus();

        ServiceProvider provider = services.build();

        try (ServiceScope scope = provider.createScope()) {
            ServiceProvider scopedServiceProvider = scope.getServiceProvider();

            MyService singleton = scopedServiceProvider.getService(MyService.class);
            MyScopedService scoped = scopedServiceProvider.getService(MyScopedService.class);

            singleton.doWork();
            scoped.doSomething();
        }
    }
}