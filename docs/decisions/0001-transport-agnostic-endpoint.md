# 0001: Adopt transport-agnostic endpoints

## Status
Accepted

## Context
MyServiceBus originally mirrored MassTransit's queue-centric transport model. To support technologies beyond brokers, the platform needs an abstraction that does not assume queues or exchanges.

## Decision
Introduce a minimal `IEndpoint` interface with capability discovery. Each transport implements `Send` and `ReadAsync` while advertising features such as acknowledgement or retry through `EndpointCapabilities`.

## Consequences
- Non-queue transports (HTTP callbacks, in-memory mediator, serverless triggers) become first-class participants.
- Capability negotiation lets higher layers opt into features only when supported.
- Existing queue-based transports remain supported via the same contract, preserving backwards compatibility.
