# ServiceBus Transport Specification

This document defines the contract for ServiceBus transports. It mirrors MassTransit's transport contract so any MyServiceBus transport can exchange messages with MassTransit. The specification is neutral to both programming language and broker implementation.

## Responsibilities

- Provide a factory that resolves send and receive transports and manages underlying connections.
- Ensure required topology (queues, exchanges, topics) exists before sending or receiving messages.
- Serialize and transmit envelopes with `content_type` defaulting to `application/vnd.masstransit+json` so they are compatible with MassTransit.
- Map headers prefixed with `_` to native transport properties.
- Propagate cancellation tokens so operations can observe shutdown or timeouts.
- Move failed messages to `<queue>_error` and publish `Fault<T>` messages describing the exception, matching MassTransit's fault-handling behavior.

## Send Transport

- Sends encoded envelopes to a destination address using the same semantics as MassTransit's send transport.
- Honors headers, correlation, and response/fault addresses.
- May be reused concurrently and must be thread safe.

## Receive Transport

- Connects to an address and feeds messages into the consume pipeline.
- Acknowledges messages once the pipeline completes successfully.
- Surfaces transport-level errors through an error queue when available, matching MassTransit's receive transport behavior.

## Examples

The RabbitMQ transport demonstrates these requirements. See [RabbitMQ Transport](rabbitmq-transport.md) for a concrete implementation.

