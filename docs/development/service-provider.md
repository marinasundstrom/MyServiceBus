# Service Providers

The Java `ServiceCollection` mirrors .NET's `IServiceCollection`. Each registration is captured as a
`ServiceDescriptor` describing the service type, implementation and lifetime. While the default
`buildServiceProvider` method produces a Guice-backed `ServiceProvider`, the descriptors can be used
to wire up any IoC container. Implementations only need to honor the `ServiceProvider` and
`ServiceScope` interfaces.

Because `ServiceCollection` itself is an interface, integrations may supply their own
implementation that collects the same registrations and materializes them in another
container. The custom `buildServiceProvider` then returns a `ServiceProvider` wrapper
around that container, as long as it implements the MyServiceBus `ServiceProvider` and
`ServiceScope` contracts. Application setup can therefore keep using the collection
and decorator programming model while an adapter for Spring, Dagger, or another
framework replaces the included Guice-backed materialization.

Adapters map MyServiceBus `SINGLETON`, `SCOPED`, and `TRANSIENT` descriptors to
the target container's native lifetime mechanisms. `ServiceProvider.createScope()`
defines the per-message boundary, and the `ServiceScope` cleanup callback keeps
framework-owned scoped instances alive through asynchronous consumption before
releasing them. Adapters must also preserve multi-bindings and pass the active scoped
provider to provider-aware factories.

Any custom `ServiceProvider` must also make itself available for injection into resolved
services. Consumers are free to declare a constructor parameter of type `ServiceProvider` and
expect the current provider instance to be supplied.

The built-in Guice implementation automatically binds `ServiceProvider` to the provider it
constructs, so your services can request it without additional configuration.

Application-facing code should not depend on Guice annotations or Guice-only APIs.
Use standard `javax.inject.Inject` for constructor injection so the same consumers
and services can be activated by alternate adapters.
