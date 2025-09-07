# MyServiceBus Architecture

MyServiceBus shares the MassTransit heritage of serialization, pipes, and transports but defines its own transport-agnostic model.

## Serialization

Messages are serialized using `System.Text.Json` by default, though other serializers can be registered. The `SendContext` and `ReceiveContext` expose transport-agnostic headers and addresses while leaving the payload shape to the configured serializer.

## Pipes and Consumers

Send, receive, and consume operations flow through a pipe-and-filter pipeline built on `BasePipeContext`. Filters compose cross-cutting concerns such as logging, retries, and tracing. Message handlers implement `IConsumer<T>` and plug into the same consume pipe, giving every transport access to shared middleware.

## Endpoints

Instead of queue- or exchange-centric transports, MyServiceBus exposes a minimal `IEndpoint` interface with `Send`, pull-based `ReadAsync` returning `ReceiveContext`, an optional push-based `Subscribe`, and advertised `EndpointCapabilities`. Producers configure metadata through `SendContext`, while consumers receive `ReceiveContext` instances. A `ConsumeContext` is created later in the pipeline when dispatching consumers. RabbitMQ implements the interface today, and other technologies—HTTP callbacks, in-memory mediators, serverless triggers—can plug in the same way.

Queue-based transports remain first-class and continue to operate through this contract for backward compatibility.

The newly added `HttpEndpoint` demonstrates this by dispatching messages to a webhook via `HttpClient` without relying on broker queues.

## Differences from MassTransit

MassTransit transports assume queues and exchanges and are created through an `ITransportFactory`. MyServiceBus replaces that assumption with the endpoint contract above, allowing transports that are not queue based. See [MassTransit Architecture](masstransit-architecture.md) for the original model.

## Message Pipeline

Regardless of transport, operations pass through filters until a transport-specific component delivers or receives the envelope. This keeps diagnostics and middleware consistent across transport types.

