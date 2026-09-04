# Client Dependencies

This document lists the primary libraries used by the reference MyServiceBus clients.

## C#

- **Serialization**: `System.Text.Json`
- **Dependency injection and configuration**: `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.Hosting`
- **Logging**: `Microsoft.Extensions.Logging`
- **Transport**: `RabbitMQ.Client`

## Java

- **Serialization**: `com.fasterxml.jackson` (`jackson-databind`, `jackson-datatype-jsr310`)
- **Dependency injection abstraction**: `javax.inject:javax.inject`
- **Default DI container implementation**: `com.google.inject:guice`
- **Logging**: `org.slf4j:slf4j-api` (examples use `slf4j-simple`)
- **Transport**: `com.rabbitmq:amqp-client`

These dependencies mirror common practices in their respective ecosystems and aim to keep the clients lightweight while remaining familiar to platform developers.

## Kotlin

- **Runtime**: the Java 17-compatible MyServiceBus JVM modules
- **Language facade**: Kotlin standard library 2.2 through `myservicebus-kotlin`
- **Asynchronous API**: `kotlinx-coroutines-core` 1.11 through `myservicebus-kotlin`
- **Serialization in the sample**: `com.fasterxml.jackson.module:jackson-module-kotlin`

The Kotlin projection is intentionally thin. Transports, topology,
serialization, and delivery semantics continue to come from the shared JVM
runtime. Kotlin applications select this projection as their application API;
Java packages remain available for explicit ecosystem interoperability.
