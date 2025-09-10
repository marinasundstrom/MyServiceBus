# Service Providers

The Java `ServiceCollection` mirrors .NET's `IServiceCollection`. Each registration is captured as a
`ServiceDescriptor` describing the service type, implementation and lifetime. While the default
`buildServiceProvider` method produces a Guice-backed `ServiceProvider`, the descriptors can be used
to wire up any IoC container. Implementations only need to honor the `ServiceProvider` and
`ServiceScope` interfaces.

Because `ServiceCollection` itself is an interface, integrations may supply their own
implementation that forwards registrations to another container. The custom
`buildServiceProvider` can then return a `ServiceProvider` wrapper around that
container, as long as it implements the MyServiceBus `ServiceProvider` and
`ServiceScope` contracts. This approach allows adapters for frameworks like
Spring or Dagger without taking a dependency on Guice.

Any custom `ServiceProvider` must also make itself available for injection into resolved
services. Consumers are free to declare a constructor parameter of type `ServiceProvider` and
expect the current provider instance to be supplied.

The built-in Guice implementation automatically binds `ServiceProvider` to the provider it
constructs, so your services can request it without additional configuration.

