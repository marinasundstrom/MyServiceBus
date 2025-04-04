# Dependency injection

Custom wrapper around Guice. Made to look similar to .NET DI.

Supporting lifetimes, scopes, and injecting `ServiceProvider`.

```java
package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;

public class Main {
    public static void main(String[] args) {
        ServiceCollection services = new ServiceCollection();
        services.addSingleton(MyService.class, MyServiceImpl.class);
        services.addScoped(MyScopedService.class);
        services.addScoped(MySecondService.class);

        ServiceProvider provider = services.build();

        try (ServiceScope scope = provider.createScope()) {
            MyService singleton = scope.getService(MyService.class);
            MyScopedService scoped = scope.getService(MyScopedService.class);

            singleton.doWork();
            scoped.doSomething();
        }
    }
}
```

Constructor injection using Guice's `@Inject`:

```java
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
```