# MVP API Surface

## Supported application APIs

The MVP support boundary is the ordinary application path in both reference clients:

- configure a bus and RabbitMQ transport
- register consumers and handlers
- start and stop the bus through the platform's hosting or lifecycle model
- send to a directed endpoint and publish by message contract
- consume, respond, retry, and surface terminal faults
- create request clients with bounded timeouts and cancellation
- configure serialization, logging, dependency injection, telemetry, and transport capability requirements
- query the normalized topology snapshot

These concepts have corresponding C# and Java APIs, but construction patterns, asynchronous types, dependency injection, and package organization remain platform-idiomatic.

## Transport extension point

New transports implement `ITransportFactory.CreateReceiveTransport(ReceiveEndpointTransportTopology, ...)` in C# or `TransportFactory.createReceiveTransport(ReceiveEndpointTransportTopology, ...)` in Java. `ReceiveEndpointTransportTopology` is the supported runtime contract for endpoint name, durability and temporary intent, bindings, prefetch, and adapter-owned options.

The older C# `ReceiveEndpointTopology` overload and Java queue/binding parameter-list overloads are deprecated compatibility adapters. They remain callable during the MVP line so existing transport implementations are not broken abruptly, but new transports must not use them as their primary contract. Removal requires a declared breaking release.

## Preview and internal surfaces

The following are not stable MVP commitments:

- dashboard HTTP endpoints and UI
- monitoring history and multi-instance discovery
- transport-projection inspection DTOs
- saga, outbox, replay, purge, and topology mutation APIs
- additional broker and event-stream profiles
- implementation registries, mutable topology objects, and pipeline internals not documented in the feature walkthrough

Preview APIs may evolve additively or be replaced before they are declared stable. Wire and RabbitMQ profile compatibility claims remain governed by the compatibility policy and conformance matrix rather than by source-level compatibility with MassTransit.
