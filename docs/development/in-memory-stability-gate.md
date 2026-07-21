# Mediator and In-Memory Stability Gate

## Purpose

Before adding another broker transport, MyServiceBus will stabilize its in-process mediator and in-memory test harness in C# and Java. They must provide predictable implementations of the same consumer, pipeline, request, fault, lifecycle, and dependency-injection concepts used by the broker-backed runtime.

This work targets MassTransit semantic and API familiarity where those promises make sense in process. It does not claim MassTransit wire or broker-topology interoperability, durability, acknowledgement, competing consumers, or redelivery.

**Status:** complete for the current preview scope. All scenarios in the shared conformance matrix have matching C# and Java coverage.

## Separate responsibilities

- The **mediator** is an application runtime for deliberately local dispatch. It invokes configured consumers and pipelines without serialization or broker delivery unless a documented option explicitly requests those boundaries.
- The **in-memory test harness** is a testing runtime. It exercises the same application-visible behavior and grows deterministic observation categories only when their contracts are defined; consumed observations are the current shared baseline.

The harness may build on shared in-memory delivery machinery, but production code must not depend on test observation APIs. The two surfaces must not drift into different dispatch semantics accidentally.

## Required conformance scenarios

Implementation status and concrete test mappings are maintained in the [Mediator and In-Memory Conformance Matrix](in-memory-conformance-matrix.md).

Matching C# and Java tests must define and verify:

1. start, stop, repeated lifecycle calls, and operations attempted outside the valid lifecycle
2. directed send to one endpoint and publish fan-out to all compatible consumers
3. consumer scope creation and disposal for every delivered message
4. request/response correlation, timeout, cancellation, and fault responses
5. retry attempt counts, delays, terminal faults, and behavior when retry is not configured
6. send, publish, and consume filters in their documented order
7. propagation of headers, correlation identifiers, cancellation, and telemetry context
8. interface and inherited message-type dispatch where supported by the portable model
9. scheduled delivery and cancellation using deterministic timing controls where practical
10. concurrent dispatch, ordering, and handler-failure behavior with explicit guarantees
11. stable topology snapshots and capability descriptors that do not imply broker primitives
12. equivalent harness observations and assertion timing in both languages

Every unsupported MassTransit mediator or in-memory feature must be documented rather than approximated silently.

## Multiple-consumer delivery contract

Each compatible consumer registration is an independent local delivery. The mediator and in-memory harness invoke every matched consumer and complete the dispatch only after all matched deliveries settle. A failure from one consumer fails the overall dispatch, but it does not suppress invocation of the other matched consumers.

No ordering is promised between independent consumers, including registration order, start order, or completion order. Filters within one consumer pipeline remain ordered according to their pipeline registration contract. Harness consumed observations represent successful consumer completions, so one message may produce multiple consumed records when multiple consumers succeed.

The shared harness exposes an eventual consumed-type observation with an explicit timeout. It checks already-recorded completions before waiting for a future completion, returns false on timeout, and preserves the platform's normal cancellation convention. This is the only shared observation category at present; sent, published, faulted, and scheduled collections require separate contracts before they are added.

Local scheduling supports both publish and directed send through the platform's message scheduler. Scheduling does not deliver before the configured job callback runs, completion of that callback includes completion of message delivery, and cancelling the handle before the callback removes the pending job. Tests use injected manual job schedulers to verify these guarantees without wall-clock sleeps. Messages with a scheduled enqueue time on their send or publish context retain timer-based local behavior; no relative ordering is promised for messages due at the same time.

## Exit criteria

The gate is complete when:

- a shared scenario matrix maps every required behavior to C# and Java tests
- both implementations pass the same semantic scenarios with documented platform-specific asynchronous APIs
- mediator and harness capabilities accurately distinguish native, emulated, and unsupported behavior
- the feature walkthrough and testing guide describe lifecycle, delivery, concurrency, and failure guarantees
- ordinary mediator and harness APIs are stable enough for the first preview package line

Only after this gate should the roadmap prioritize a second broker transport. Inspection and monitoring can continue as optional work, but they must not displace correctness of the application runtime.
