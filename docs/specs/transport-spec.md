# ServiceBus Transport Specification

This document defines the contract for ServiceBus transports. It provides portable messaging semantics and allows a named transport profile to mirror MassTransit's addressing, topology, and settlement behavior. Compatibility is claimed and tested per profile; implementing the generic transport contract alone does not guarantee interoperability with every MassTransit transport. The specification is neutral to both programming language and broker implementation.

See [MyServiceBus Architecture](../myservicebus-architecture.md) for the distinction between bus transports, event streams, hosting adapters, and application integrations.

## Capability Descriptor

Every transport must publish a machine-readable descriptor for the capabilities it supports. A capability is classified as:

- `native` when the underlying transport provides the behavior directly
- `emulated` when MyServiceBus implements the behavior and documents its limitations
- `unsupported` when configuration requiring the behavior must be rejected

The descriptor should cover directed send, publish/subscribe, durability, competing consumers or consumer groups, acknowledgement or checkpointing, request/response suitability, scheduling, redelivery, error destinations, ordering scope, replay, temporary endpoints, and topology provisioning.

The runtime must validate endpoint and bus configuration against the descriptor before startup. It must not silently replace a requested delivery guarantee with weaker behavior.

## Transport Profiles

A MassTransit-compatible transport profile adds concrete rules to the generic contract:

- supported URI schemes and address normalization
- entity naming and topology mapping
- native header and property mapping
- publish, send, and subscription conventions
- acknowledgement, rejection, and redelivery behavior
- error, skipped, and fault destination conventions
- temporary endpoint and request/response behavior

RabbitMQ, Azure Service Bus, SQS/SNS, and other profiles are independent conformance targets. Portable behavior may be shared even when topology rules differ.

## Responsibilities

- Provide a factory that resolves send and receive transports and manages underlying connections.
- Ensure required topology exists before sending or receiving messages when topology provisioning is supported and enabled.
- Serialize and transmit envelopes with `content_type` defaulting to `application/vnd.masstransit+json` so they are compatible with MassTransit. Transports may also send raw JSON with `content_type=application/json` when a raw serializer is explicitly selected. Receive paths must continue to support envelope messages by default and may dispatch raw `application/json` messages for endpoints that are explicitly configured for raw consumption.
- Map headers prefixed with `_` to native transport properties.
- Propagate cancellation tokens so operations can observe shutdown or timeouts.
- Apply the selected profile's error and fault conventions. For the RabbitMQ profile, move failed messages to `<queue>_error` and publish `Fault<T>` messages describing the exception using the documented MassTransit-compatible routing rules.

## Send Transport

- Sends encoded envelopes to a destination address using the same semantics as MassTransit's send transport.
- Honors headers, correlation, and response/fault addresses.
- May be reused concurrently and must be thread safe.

## Receive Transport

- Connects to an address and feeds messages into the consume pipeline.
- Acknowledges or checkpoints messages once the pipeline completes successfully, according to the transport's settlement model.
- Surfaces transport-level errors through the configured error mechanism when available, according to the selected transport profile.

## Event Streams and Integrations

Event streams may reuse envelopes, serialization, consume pipelines, and telemetry, but should expose stream-native concepts such as topics, partitions, keys, offsets, checkpoints, and consumer groups. They are not required to emulate directed queues or temporary response endpoints.

Transient application technologies such as SignalR are integrations, not implementations of this transport contract. They should bridge durable bus messages to their native delivery model without advertising durability or settlement guarantees they do not provide.

## Examples

The RabbitMQ transport demonstrates these requirements. See [RabbitMQ Transport](rabbitmq-transport.md) for a concrete implementation.
