# Dependency injection

Custom wrapper around Guice. Made to look similar to .NET DI.

`ServiceCollection.create()` constructs a `DefaultServiceCollection`
backed by [Guice](https://github.com/google/guice), so the default container
uses Guice under the hood while presenting a .NET-like API.

Supporting lifetimes, scopes, and injecting `ServiceProvider`.

The default container registers the `ServiceProvider` itself, allowing any
service to take `ServiceProvider` as a dependency and use it to resolve
additional services.

Use `getService` when a missing binding is acceptable or `getRequiredService`
to throw an exception if the service isn't registered, mirroring .NET's
`GetRequiredService`.

### ServiceCollection

`ServiceCollection` gathers service registrations before the `ServiceProvider`
is built. It mirrors .NET's `IServiceCollection` and exposes methods to add
singletons, scoped services, or multi-bindings. Because it implements
`Iterable<ServiceDescriptor>`, the registrations can be inspected or modified
up front:

```java
ServiceCollection services = ServiceCollection.create();
services.addSingleton(MyService.class, MyServiceImpl.class);
services.addScoped(MyScopedService.class);

services.remove(MyService.class); // optional removal

for (com.myservicebus.di.ServiceDescriptor d : services.getDescriptors()) {
    System.out.println(d.getServiceType());
}
```

Once `buildServiceProvider` or `connectAndBuild` is called the collection is
frozen and further modifications throw an `IllegalStateException`.

### ServiceDescriptor

Each registration in the collection is represented by a `ServiceDescriptor`
containing the service type, implementation type or factory, optional instance,
the service lifetime (`SINGLETON` or `SCOPED`), and whether it participates in
multi-binding. Descriptors are primarily consumed when building the underlying
Guice injector but can also be used for diagnostics or tooling.

### Basic usage

```java
package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;

public class Main {
    public static void main(String[] args) {
        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(MyService.class, MyServiceImpl.class);
        services.addScoped(MyScopedService.class);
        services.addScoped(MySecondService.class);

        ServiceProvider provider = services.buildServiceProvider();

        try (ServiceScope scope = provider.createScope()) {
            var scopedSp = scope.getServiceProvider();

            MyService singleton = scopedSp.getRequiredService(MyService.class);
            MyScopedService scoped = scopedSp.getRequiredService(MyScopedService.class);

            singleton.doWork();
            scoped.doSomething();
        }
    }
}
```

### Connecting to an existing Guice injector

If you already have a Guice `Injector`, `DefaultServiceCollection` can reuse it via `connectAndBuild`:

```java
import com.google.inject.Guice;
import com.google.inject.Injector;
import com.myservicebus.di.DefaultServiceCollection;
import com.myservicebus.di.ServiceProvider;

Injector existing = Guice.createInjector();
DefaultServiceCollection services = new DefaultServiceCollection();
services.addSingleton(MyService.class, MyServiceImpl.class);

ServiceProvider provider = services.connectAndBuild(existing);
```

### Registering bindings for factories

Sometimes the service you want to register is itself a factory. Use the
overload that accepts a lambda so the factory can resolve any dependencies
from the `ServiceProvider` before constructing itself:

```java
services.addSingleton(WidgetFactory.class, sp -> () -> {
    Dependency dep = sp.getRequiredService(Dependency.class);
    return new WidgetFactory(dep);
});

WidgetFactory factory = provider.getService(WidgetFactory.class);
Widget widget = factory.create();
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
        var secondService = serviceProvider.getRequiredService(MySecondService.class);
        secondService.doSomething();
    }
}
```
