# Java dependency-injection boundary

MyServiceBus defines a small `ServiceCollection`, `ServiceProvider`, and `ServiceScope` programming model. Applications can use that model with the included Guice-backed implementation or materialize the same registrations through an adapter backed by another dependency-injection framework. Guice is not part of the abstraction.

## Existing-application integration: factories

The primary integration boundary is the bus factory. An endpoint handler can close over Spring, CDI, Dagger, Guice, a framework-specific resolver, or an application-owned object graph without exposing MyServiceBus DI at all:

```java
MessageBus bus = MessageBus.factory.create(RabbitMqFactoryConfigurator.class, cfg -> {
    cfg.host("localhost");
    cfg.receiveEndpoint("submit-order", endpoint ->
            endpoint.handler(SubmitOrder.class, context ->
                    applicationContext.getBean(SubmitOrderConsumer.class).consume(context)));
});
```

When a bus extension needs an application service, an explicit service factory can be contained within the same factory setup:

```java
var applicationContext = createApplicationContext();

MessageBus.factory
        .configureServices(services -> services.addScoped(
                OrderRepository.class,
                () -> applicationContext.getBean(OrderRepository.class)))
        .create(RabbitMqFactoryConfigurator.class, bus -> bus.host("localhost"));
```

The application container remains responsible for constructing the consumer and injecting its normal services. The MyServiceBus provider is contained within bus setup and runtime plumbing; application code does not implement or resolve through it.

For a new MyServiceBus-first application, prefer the established `ServiceCollection.from(...)` decorator style. It lets MyServiceBus structure the service graph in a way that resembles the C# setup. Use the factory as the default boundary when adding MyServiceBus to an existing Java system whose container and construction model should remain in charge.

## Framework integration examples

These examples deliberately keep the existing container outside the MyServiceBus provider. The container constructs the consumer and injects its ordinary application services; the bus factory captures that managed consumer at the application composition root.

### Spring

Define the bus as a Spring bean. Spring injects the application consumer, and the bean lifecycle starts and stops the bus:

```java
@Configuration
class MessagingConfiguration {
    @Bean(initMethod = "start", destroyMethod = "stop")
    MessageBus messageBus(SubmitOrderConsumer consumer) {
        return MessageBus.factory.create(
                RabbitMqFactoryConfigurator.class,
                rabbit -> {
                    rabbit.host("localhost");
                    rabbit.receiveEndpoint("submit-order", endpoint ->
                            endpoint.handler(SubmitOrder.class, consumer::consume));
                });
    }
}

@Component
final class SubmitOrderConsumer {
    private final OrderRepository orders;

    SubmitOrderConsumer(OrderRepository orders) {
        this.orders = orders;
    }

    CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) {
        return orders.submit(context.getMessage());
    }
}
```

### Jakarta CDI

A CDI producer can start the bus from an injected application-scoped consumer, and a disposer method stops it:

```java
@ApplicationScoped
class MessagingConfiguration {
    @Produces
    @ApplicationScoped
    MessageBus messageBus(SubmitOrderConsumer consumer) throws Exception {
        MessageBus bus = MessageBus.factory.create(
                RabbitMqFactoryConfigurator.class,
                rabbit -> {
                    rabbit.host("localhost");
                    rabbit.receiveEndpoint("submit-order", endpoint ->
                            endpoint.handler(SubmitOrder.class, consumer::consume));
                });
        bus.start();
        return bus;
    }

    void stop(@Disposes MessageBus bus) throws Exception {
        bus.stop();
    }
}
```

The consumer can use normal CDI injection and scopes. The example applies to CDI-based runtimes such as Weld and Quarkus without introducing a Guice bridge.

### Dagger

Expose the consumer as an entry point on the generated component and capture it in the bus configuration:

```java
@Singleton
@Component
interface ApplicationComponent {
    SubmitOrderConsumer submitOrderConsumer();
}

ApplicationComponent application = DaggerApplicationComponent.create();
SubmitOrderConsumer consumer = application.submitOrderConsumer();

MessageBus bus = MessageBus.factory.create(
        RabbitMqFactoryConfigurator.class,
        rabbit -> {
            rabbit.host("localhost");
            rabbit.receiveEndpoint("submit-order", endpoint ->
                    endpoint.handler(SubmitOrder.class, consumer::consume));
        });

bus.start();
```

For prototype, dependent, request, or other shorter-lived consumers, capture an application-owned factory instead of one consumer instance. The target container must remain responsible for matching creation and cleanup; MyServiceBus should not guess another framework's lifetime semantics.

Use the provider-aware overload only when one factory needs another MyServiceBus registration:

```java
services.addScoped(OrderHandler.class,
        provider -> () -> new OrderHandler(
                provider.getRequiredService(OrderRepository.class)));
```

## Included implementations

- `ServiceCollection.create()` is the conventional implementation. It uses Guice internally to provide constructor injection and class-based registrations.
- `ServiceCollection.createAot()` is factory-only. It performs no reflective constructor activation and is used by the GraalVM Native Image smoke application.

The factory-only implementation requires explicit registrations:

```java
ServiceCollection services = ServiceCollection.createAot();
services.addSingleton(OrderRepository.class,
        () -> applicationContext.getBean(OrderRepository.class));
```

A class-only registration in this mode fails when resolved and explains that an explicit provider factory is required. This makes an accidental reflection dependency visible during development.

## Materializing the collection with another container

A framework integration can implement `ServiceCollection` to collect or forward the same registrations produced by MyServiceBus decorators, then return a `ServiceProvider` backed by its container from `buildServiceProvider()`. This keeps the MyServiceBus programming model while changing how the collection is materialized, much like using another provider behind the .NET dependency-injection abstractions.

The runtime resolves services only through `ServiceProvider`, including multi-bindings and message scopes. A custom provider creates a neutral `ServiceScope` with its scoped provider and cleanup callback:

```java
return new ServiceScope(scopedProvider, scopedContainer::close);
```

An adapter maps the MyServiceBus semantics onto the closest native concepts in its framework:

| MyServiceBus contract | Adapter responsibility |
| --- | --- |
| `SINGLETON` | Return one instance for the root provider and all of its message scopes; dispose it when the owning container shuts down. |
| `SCOPED` | Return one instance within each `createScope()` boundary. MyServiceBus creates that boundary for a message and closes it after asynchronous consumption completes. |
| `TRANSIENT` | Create a new instance for each resolution and apply the target framework's ownership rules. |
| `ServiceScope` | Wrap the framework's child/request/dependent scope and supply an idempotent cleanup callback. The scoped provider must remain usable until `close()`, including after `detach()` hands lifetime ownership to asynchronous work. |
| Provider-aware factory | Invoke `create(scopedProvider)` with the provider for the active lifetime, then call the returned `Supplier` according to the descriptor lifetime. |
| Multi-binding | Preserve every registration and return the complete set from `getServices(...)`, rather than selecting one binding. |
| `ServiceProvider` resolution | Resolve through the adapter wrapper and make the current provider available when application services request it. |

The target framework does not need to use the same names or implement scopes internally in the same way. The adapter is responsible for preserving these observable lifetime and resolution guarantees.

Zero-argument registrations use the JDK-standard `Supplier<? extends T>` contract. Provider-aware registrations use the MyServiceBus-owned `ServiceProviderBasedProvider`, which also returns a `Supplier`. Neither contract exposes a Guice type. `javax.inject.Provider<T>`, `jakarta.inject.Provider<T>`, Spring `ObjectProvider<T>`, and other factory-shaped APIs adapt without a package dependency through method references:

```java
services.addScoped(OrderHandler.class, frameworkProvider::get);
```

`ServiceDescriptor` supplies the service type, implementation type or factory, lifetime, and multi-binding flag needed by a full adapter. Java has no standardized equivalent of .NET's complete `IServiceProvider` and scope model, so MyServiceBus retains its own neutral runtime abstraction rather than treating a single-service `Provider<T>` as a container.

The reflective `from(Class<T>)` decorator convenience is implementation-specific. An adapter intended for the conventional programming model should support it or provide an equivalent decorator-construction mechanism; the factory-only AOT implementation deliberately rejects it. Core bus setup accepts any `ServiceCollection` implementation directly.

Guice remains an included default adapter for compatibility. It is an implementation dependency rather than part of the provider-factory contract. A future packaging slice may move that adapter to a separate artifact without changing the MyServiceBus DI interfaces.

See the official [Spring container](https://docs.spring.io/spring-framework/reference/core/beans/basics.html), [Jakarta CDI](https://jakarta.ee/specifications/cdi/4.1/), and [Dagger component](https://dagger.dev/dev-guide/basic-usage) documentation for their framework-specific construction and lifetime rules.
