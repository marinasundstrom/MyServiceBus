# Service Providers

The Java `ServiceCollection` mirrors .NET's `IServiceCollection`. Each registration is captured as a
`ServiceDescriptor` describing the service type, implementation and lifetime. While the default
`buildServiceProvider` method produces a Guice-backed `ServiceProvider`, the descriptors can be used
to wire up any IoC container. Implementations only need to honor the `ServiceProvider` and
`ServiceScope` interfaces.

Any custom `ServiceProvider` must also make itself available for injection into resolved
services. Consumers are free to declare a constructor parameter of type `ServiceProvider` and
expect the current provider instance to be supplied.

The built-in Guice implementation automatically binds `ServiceProvider` to the provider it
constructs, so your services can request it without additional configuration.

