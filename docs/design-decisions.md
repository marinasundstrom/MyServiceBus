# Design Decisions

This document explains why some MyServiceBus APIs differ from MassTransit and highlights key differences between the C# and Java examples in the [quick-start](quick-start.md).

## C# deviations from MassTransit

- **Unified bus interface** – MyServiceBus exposes a single `IMessageBus` for sending, publishing, and request/response instead of MassTransit's `IBus`/`IBusControl`. This keeps the API lightweight and matches the cross-language design shared with the Java client.
- **Simplified configuration** – Rather than `AddMassTransit` and separate configuration builders, the C# client uses `AddServiceBus` with transport-specific configurators. The goal is to reduce ceremony while retaining familiar MassTransit concepts.

## Java-specific differences

- **Manual bus lifecycle** – The Java examples configure the bus with `RabbitMqBusFactory.configure`, resolve a `ServiceBus` from the container, and call `start()` explicitly. Unlike ASP.NET's hosting model, Java lacks a standardized host so the bus must manage its own lifecycle.
- **Asynchronous style** – C# relies on `async`/`await` with `Task`, while Java returns `CompletableFuture` and callers often invoke `.join()`. This pattern reflects Java's lack of a language-level async keyword.
- **Cancellation tokens** – Operations in Java require an explicit `CancellationToken.none` because the JDK has no built-in cancellation primitive comparable to .NET's `CancellationToken` parameter defaults.
- **Endpoint resolution** – C# examples resolve `ISendEndpoint` from DI. Java acquires a `SendEndpoint` via a `SendEndpointProvider` and a URI, mirroring how MassTransit addresses endpoints but adapted for Java's type system.
- **Request/response helpers** – The C# client injects `IRequestClient<T>`. Java creates a client through `RequestClientFactory` because Java cannot infer generic interfaces the same way.
- **Testing** – Both platforms provide an in-memory test harness. The Java harness mirrors the C# API but returns `CompletableFuture` for each operation, requiring explicit coordination.

These differences stem from language and platform constraints rather than divergent messaging semantics. Both clients aim to stay aligned conceptually so moving between them remains straightforward.

## Dependency choices

The .NET client builds on `System.Text.Json` and the `Microsoft.Extensions` stack for configuration, dependency injection, and logging. The Java client mirrors these roles with Jackson, Guice, and SLF4J. Both implementations use the RabbitMQ client provided by their platform. See [client dependencies](dependencies.md) for a full list.
