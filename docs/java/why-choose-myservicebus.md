# Why Choose MyServiceBus for Java

MyServiceBus brings a MassTransit-compatible message bus to the JVM with a focus on
cross-language interoperability and minimal dependencies. It supports two primary adoption paths:

1. Add a Java service to an existing MassTransit-based .NET estate using the documented common interoperability subset.
2. Start a greenfield C# and/or Java system with the same focused messaging model on both platforms.

Consider MyServiceBus when you need:

- **Cross-platform services** – run C# and Java consumers side-by-side while sharing contracts and transports.
- **Lightweight runtime** – the Java client relies only on small DI and logging abstractions, keeping deployments slim.
- **Familiar concepts** – MassTransit experience transfers directly; configuration and messaging patterns mirror the .NET world.
- **Configurable retries** – opt into retry policies through filters to handle transient failures.
- **Explicit control** – applications start and stop the bus manually, providing deterministic lifecycle management.

These characteristics make MyServiceBus a pragmatic option for Java teams integrating with existing MassTransit ecosystems or introducing a unified bus across languages. Its MIT license may also fit projects whose requirements or current stage do not justify MassTransit v9's commercial license, support, and broader feature set. That advantage comes with a real trade-off: MyServiceBus is a preview and does not claim MassTransit's maturity or full enterprise breadth.

The currently verified MassTransit peer is version 8.5.1. See the cross-platform [Why Choose MyServiceBus?](../why-myservicebus.md) guide for the complete technical and commercial decision boundary.
