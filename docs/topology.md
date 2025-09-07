# Topology

MyServiceBus uses topology objects to describe how messages and consumers map to the underlying transport. The bus builds this information during configuration and transports use it to declare queues, exchanges and bindings.

## Bus Topology
The bus maintains a registry of all message and consumer mappings. `BusRegistrationConfigurator` registers entries in a `TopologyRegistry`, and `IMessageBus.Topology` exposes the resulting `MessageTopology` and `ConsumerTopology` collections.

## Message Topology
`MessageTopology` links a .NET or Java type to the exchange or topic name used on the broker. Consumers automatically add entries for the messages they handle, or you can call `RegisterMessage<T>(string entityName)` to supply a custom name.

## Consumer Topology
`ConsumerTopology` captures the queue a consumer listens on and which message types are bound to that queue. It also stores optional settings such as `PrefetchCount` and any additional filters configured for the consumer pipeline.

## Transport Interaction
Before a transport begins sending or receiving messages it ensures the required exchanges, queues and bindings exist according to the registered topology. When the bus starts, each consumer entry is activated so incoming messages flow through the configured pipeline.

## User Guidance
- Use `AddConsumer` on `BusRegistrationConfigurator` to register consumers; message and consumer topology entries are generated automatically.
- Inspect `IMessageBus.Topology` for a runtime view of registered messages and consumers.
- Register messages explicitly when you need to customize exchange names or other entity identifiers.
- Adjust `PrefetchCount` on your transport or receive endpoint to tune throughput.
