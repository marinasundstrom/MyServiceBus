# MyServiceBus Architecture

MyServiceBus is composed of three layers that work together to deliver messages:

## Configuration

The configuration layer defines how applications integrate with the bus.

- Options objects capture transport-agnostic settings such as queue names or broker hosts.
- Fluent extension methods (e.g. `AddServiceBus`, `AddConsumer`) provide idiomatic registration patterns.
- Dependency injection wires transports, serialization, and filters through `IServiceCollection` so they can be replaced or tested.

## Transports

Transports move envelopes between endpoints. Each transport implementation is configured through the options and extension methods above. RabbitMQ and an in-memory mediator are provided, and additional transports can be added by implementing the `ITransportFactory` abstraction.

## Bus Abstraction

The bus exposes a consistent API regardless of transport.

- **Producers and Consumers** manage sending and receiving.
- **Send** directs a message to a specific queue.
- **Publish** fans out a message to multiple subscribers.
- **Request/Response** enables RPC-style exchanges.

All messaging operations flow through the pipe-and-filter pipeline so filters can handle cross-cutting concerns such as logging or retries.
