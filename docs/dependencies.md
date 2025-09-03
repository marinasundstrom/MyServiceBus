# Client Dependencies

This document lists the primary libraries used by the reference MyServiceBus clients.

## C#

- **Serialization**: `System.Text.Json`
- **Dependency injection and configuration**: `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.Hosting`
- **Logging**: `Microsoft.Extensions.Logging`
- **Transport**: `RabbitMQ.Client`

## Java

- **Serialization**: `com.fasterxml.jackson` (`jackson-databind`, `jackson-datatype-jsr310`)
- **Dependency injection**: `com.google.inject:guice`
- **Logging**: `org.slf4j:slf4j-api` (examples use `slf4j-simple`)
- **Transport**: `com.rabbitmq:amqp-client`

These dependencies mirror common practices in their respective ecosystems and aim to keep the clients lightweight while remaining familiar to platform developers.
