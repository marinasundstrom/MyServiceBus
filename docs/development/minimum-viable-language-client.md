# Minimum Viable Client for Another Language

Status: investigation and implementation guide<br>
Last reviewed: 2026-08-29<br>
Initial transport profile: RabbitMQ AMQP 0-9-1<br>
Candidate languages: TypeScript/JavaScript, Go, Rust, and Python

## Purpose

This document defines the smallest language-neutral implementation that lets another programming language participate safely in message exchange with MyServiceBus C# and Java. It deliberately separates a thin interoperability participant from a complete MyServiceBus client.

The goal is not to translate the C# or Java runtime. The goal is to validate that the protocol, contracts, transport profile, and conformance suite are sufficiently explicit that an independent implementation can:

- publish and receive the sample contracts;
- send to a named endpoint;
- preserve message identity and conversation metadata;
- respond to a request from a C# or Java caller;
- settle successful deliveries correctly; and
- prove those behaviors through shared fixtures and executable cross-language tests.

This is a maintainer document, not a commitment to ship four additional clients.

## Conclusion

The minimum useful implementation consists of five small pieces:

1. **Explicit contract registry** — maps a local type or decoder to a language-neutral message URN, entity name, and JSON codec.
2. **Envelope codec** — reads and writes protocol-v1 MassTransit-compatible JSON envelopes.
3. **One transport-profile adapter** — initially RabbitMQ AMQP 0-9-1 with publisher confirms, manual consumer acknowledgement, and pre-provisioned topology.
4. **Minimal operation context** — exposes the consumed message and metadata and can publish, send, and respond while preserving conversation rules.
5. **Interop peer and conformance runner** — a small executable driven by the existing .NET tests in both directions.

The first milestone should be a thin peer, not a general-purpose package. It should implement `publish`, directed `send`, `consume`, and `respond`. A request client, fault production, retries, error/skipped queues, topology provisioning, dependency injection, pipelines, scheduling, outbox, monitoring, and inspection can follow independently.

Go is the recommended first protocol-validation language because it has a RabbitMQ-team-maintained AMQP 0-9-1 client, produces a simple standalone peer executable, has an idiomatic cancellation/deadline model, and forces the design to work without CLR/JVM reflection or dependency injection. TypeScript or Python would be the fastest proof that dynamic-language applications can participate. Rust is valuable after the boundary is stable because its ownership model will stress acknowledgement, connection, and shutdown lifetimes, but it is not the lowest-effort first implementation.

## What “participate” means

Participation is intentionally narrower than full compatibility.

### Minimum exchange participant

A minimum exchange participant can:

- read the shared envelope fixtures;
- emit an envelope accepted by both reference clients;
- publish an event to a known contract exchange;
- send a command to a known endpoint;
- consume a known contract from a pre-provisioned queue;
- acknowledge only after the asynchronous handler succeeds;
- respond to an incoming request using its response address and request identifier; and
- start, report readiness, drain in-flight work, and stop.

It may initially declare these capabilities unsupported:

- creating broker topology;
- originating requests;
- producing `Fault<T>`;
- retry and terminal error handling;
- skipped-message handling;
- scheduling;
- outbox and inbox;
- filters and dependency-injection scopes;
- topology inspection and monitoring export;
- Azure Service Bus; and
- MassTransit compatibility beyond the exact scenarios in its conformance matrix.

Unsupported behavior must be absent or rejected. It must not be silently approximated and advertised as supported.

### Not yet a MyServiceBus reference client

Passing envelope and sample-exchange tests is a Level 1 wire and scoped RabbitMQ interoperability result. It does not establish full Level 2 semantic compatibility, the complete RabbitMQ transport profile, Azure Service Bus support, or cross-language feature parity.

The implementation should be described as an **experimental exchange participant** until its named maturity stage passes. Avoid calling it a complete MyServiceBus client or generally MassTransit-compatible.

## Progressive maturity stages

These `M` stages describe implementation scope. They are deliberately distinct from the repository's numbered [compatibility levels](../compatibility.md), which describe evidence-backed interoperability promises.

| Level | Name | Required capability | Claim permitted |
| --- | --- | --- | --- |
| M0 | Fixture reader | Parse protocol manifest and message, request, and fault fixtures | Envelope codec experiment |
| M1 | Envelope producer | Emit compatible envelope JSON and native content type | Wire producer for named contracts |
| M2 | Exchange participant | Publish, directed send, consume, respond to incoming requests, manual acknowledgement, lifecycle | Experimental RabbitMQ exchange participant and responder |
| M3 | Conversation participant | Originate requests; decode and produce faults; enforce timeouts | Request/response participant for named contracts |
| M4 | RabbitMQ profile client | Own topology, publisher confirms, mandatory routing, recovery, retry, `_error`, `_skipped`, `_fault`, graceful drain | Compatible with documented RabbitMQ-profile limitations |
| M5 | Portable MyServiceBus client | Portable send/publish/consume/request semantics, capability descriptor, conformance with C# and Java | Cross-language client for its documented capability set |
| M6 | Reference client | Release, support, package, AOT/native where applicable, monitoring/inspection, complete required matrices | Reference implementation |

Progress is cumulative. A language can remain useful at M2 or M3 without copying the entire C# and Java product surface.

## Language-neutral core

### 1. Contract descriptor

Local namespaces and package names cannot define cross-language contract identity. JavaScript modules, Go packages, Rust crates, and Python modules will not naturally reproduce a CLR namespace. Every third-language contract must therefore bind explicitly to its wire identity.

Conceptually:

```text
Contract<T>
  logical name
  message URN
  publish entity name
  encode(T) -> JSON value
  decode(JSON value) -> T or validation error
  optional implemented-contract URNs
```

For the current sample model:

| Contract | Message URN | RabbitMQ publish entity | Payload |
| --- | --- | --- | --- |
| `SubmitOrder` | `urn:message:TestApp:SubmitOrder` | `TestApp:SubmitOrder` | `orderId`, `message` |
| `OrderSubmitted` | `urn:message:TestApp:OrderSubmitted` | `TestApp:OrderSubmitted` | `orderId`, `replica` |
| `TestRequest` | `urn:message:TestApp:TestRequest` | `TestApp:TestRequest` | `message` |
| `TestResponse` | `urn:message:TestApp:TestResponse` | `TestApp:TestResponse` | `message` |
| `Fault<TestRequest>` | `urn:message:MassTransit:Fault[[TestApp:TestRequest]]` | profile-defined fault destination | Standard fault payload containing the original request and exception information |

These identities should eventually live in a checked-in, language-neutral contract manifest rather than being copied from this table. Reflection-based convention can remain a convenience in C# and Java, but explicit registration is the portable source of truth.

The registry must reject:

- duplicate URNs bound to incompatible codecs;
- duplicate logical names with different wire identities;
- empty entity names or URNs;
- payloads that fail the registered decoder; and
- attempts to publish an unregistered local type.

### 2. Envelope codec

The codec must support the protocol-v1 fields already represented by `test/fixtures/protocol/v1`:

```text
messageId           UUID, required for outbound messages
requestId           UUID or null
correlationId       UUID or null
conversationId      UUID or null
initiatorId         UUID or null
sourceAddress       URI string or null
destinationAddress  URI string or null
responseAddress     URI string or null
faultAddress        URI string or null
expirationTime      UTC/offset timestamp or null
sentTime            UTC/offset timestamp or null
messageType         ordered array of message URNs
message             registered JSON payload
headers             JSON object
host                host object or null
contentType         inner payload content type, currently application/json
```

For predictable first implementations, write all known fields and use JSON `null` for absent optional values. Readers must ignore unknown envelope and host fields so protocol additions remain forward-compatible. JSON object-property order is not significant.

Outbound defaults are:

- a new UUID `messageId`;
- a new UUID `conversationId` when no consumed conversation exists;
- current UTC `sentTime`;
- one or more explicitly registered `messageType` URNs;
- an empty `headers` object when no application headers exist;
- `host: null` for the minimum peer; and
- envelope `contentType: "application/json"`.

The AMQP message content type is different from the envelope's inner `contentType`: publish the body with native AMQP `content_type` set to `application/vnd.masstransit+json`.

UUIDs must use the normal hyphenated textual representation. Timestamps must use an ISO-8601/RFC-3339-compatible offset representation accepted by both reference codecs. Header values in the minimum profile should be limited to null, strings, booleans, finite JSON numbers, arrays, and string-keyed objects until the cross-language header type rules are formalized.

### 3. Conversation propagation

The portable context must implement these rules:

- A new top-level send or publish creates a new `conversationId` unless the caller supplies one.
- A publish, send, response, or fault initiated while consuming retains the consumed `conversationId`.
- A consumer-initiated outbound message uses the consumed `correlationId` as `initiatorId` when one exists.
- It does not automatically reuse the consumed `correlationId` as the new outbound `correlationId`.
- A response retains the incoming `requestId`.
- A response is sent to `responseAddress`; a fault uses `faultAddress` when set, otherwise `responseAddress`.
- Expired requests must not wait indefinitely. A requester applies a local deadline even when broker expiration is also used.

The context should expose metadata explicitly. Do not use thread-local, task-local, goroutine-local, or ambient global state as the authoritative propagation mechanism.

### 4. Minimal operation surface

The common conceptual API is:

```text
publish(contract, message, options)
send(destination, contract, message, options)
consume(endpoint, contract, handler)

ConsumeContext<T>
  message
  envelope metadata
  application headers
  publish(contract, message, options)
  send(destination, contract, message, options)
  respond(contract, message, options)
```

`request` belongs to M3. It can be layered over send, a temporary response endpoint, `requestId` matching, timeout, and fault recognition after M2 is stable.

Avoid exposing transport classes, exchange declarations, serializer internals, dependency-injection hooks, filters, retries, or monitoring in the first public surface.

### 5. Error model

The minimum library needs distinct error categories even if each language represents them differently:

- configuration or invalid contract;
- serialization or validation;
- connection or channel failure;
- unroutable publish;
- publisher confirm failure or ambiguous confirmation;
- handler failure;
- settlement failure;
- cancellation;
- request timeout; and
- remote request fault.

Preserve the original library/runtime exception as a cause where the language supports exception chaining. A failed publish must not be reported as accepted merely because bytes were handed to the client library.

## Minimal RabbitMQ AMQP 0-9-1 profile

RabbitMQ is the appropriate first transport because the repository already has a pinned Testcontainers profile and C#/Java/MassTransit interoperability matrix, and all four candidate languages have viable AMQP 0-9-1 libraries.

Do not implement the new participant with RabbitMQ's AMQP 1.0 clients. AMQP 1.0 is a different transport mapping and does not automatically satisfy the existing MyServiceBus AMQP 0-9-1 exchange, queue, property, and settlement profile.

### Transport library candidates

| Language | Candidate | Notes |
| --- | --- | --- |
| TypeScript/Node.js | `amqplib` | Used by RabbitMQ's JavaScript AMQP 0-9-1 tutorials; promise and callback APIs. Version compatibility with the pinned RabbitMQ line must be checked. |
| Go | `github.com/rabbitmq/amqp091-go` | Maintained by the RabbitMQ team; supports contexts in publishing APIs and is the recommended first peer dependency. |
| Rust | `amqprs` or `lapin` | Community clients listed by RabbitMQ. Select one through a small spike covering confirms, mandatory returns, consumer cancellation, recovery expectations, and TLS. |
| Python | `pika` for the direct RabbitMQ tutorial path, or `aio-pika` for an asyncio-shaped client | `aio-pika` offers async APIs and robust connection/topology recovery but adds a community wrapper dependency. |

The MyServiceBus API should wrap the chosen client narrowly so a future library change does not affect contract or envelope APIs.

### Pre-provisioned M2 topology

The conformance host should provision topology for the first peer:

- durable fanout contract exchange named by the explicit contract entity;
- durable endpoint queue;
- durable fanout endpoint exchange with the same name as the queue;
- endpoint queue bound to its endpoint exchange with an empty routing key; and
- endpoint queue bound to each consumed contract exchange with an empty routing key.

The participant only connects, publishes, and consumes. This eliminates topology-declaration drift while the protocol implementation is being validated.

At M4 the client must declare the same topology itself and add `<queue>_error`, `<queue>_skipped`, and `<queue>_fault` fanout exchanges and queues according to the RabbitMQ profile.

### Publish and directed send

- **Publish** sends to the registered durable fanout contract exchange with an empty routing key.
- **Endpoint send** sends to the durable fanout exchange named after the endpoint, with an empty routing key.
- A direct default-exchange queue send can be added as an explicit `queue:` address optimization, but it is not required for the first sample.
- Set delivery mode to persistent for durable sends.
- Enable publisher confirms and wait for acceptance.
- Use mandatory publishing for paths where routing is required and surface returned messages as an unroutable error.
- Do not automatically replay a publish whose confirmation outcome is ambiguous.

### Consume and settlement

- Use manual acknowledgement.
- Apply a small prefetch value and a bounded handler-concurrency limit; the initial peer can use one for both.
- Decode and validate the envelope before invoking application code.
- Dispatch only when a registered message URN is present.
- Acknowledge after the async handler and any awaited response/publication complete successfully.
- On cancellation, stop accepting new deliveries and drain in-flight handlers before closing the channel.

For M2 experiments, a handler failure may negatively acknowledge and requeue while the test suite exercises only successful handling. This is not sufficient for production because poison messages can loop indefinitely. M4 must implement the documented retry and terminal `_error` behavior and preserve the original envelope.

### Response

An M2 responder reads `responseAddress` and `requestId` from the incoming envelope. It builds a `TestResponse` envelope that:

- uses the response contract URN;
- preserves the incoming `requestId`;
- preserves the incoming `conversationId`;
- sets `initiatorId` from the incoming `correlationId` when present;
- uses a new `messageId` and `sentTime`; and
- publishes to the response exchange represented by `responseAddress`.

The initial peer only needs to respond to a request originated by C# or Java. Originating a request requires temporary topology ownership, correlation isolation, timeout, late-response handling, cleanup, and fault decoding, so it belongs to M3.

## Language-specific projections

The shared semantics should look native in each language.

### TypeScript / JavaScript

- Prefer TypeScript for the package surface even if the runtime is ordinary Node.js JavaScript.
- Represent asynchronous operations with `Promise`.
- Accept `AbortSignal` in per-operation options for cancellation and deadlines.
- Define `Contract<T>` with explicit `urn`, `entityName`, `encode`, and `decode`; never use `constructor.name` as wire identity.
- Treat runtime payload validation as part of decoding. Allow adapters for validation libraries, but keep the core codec dependency-light.
- Model a subscription as an asynchronously disposable handle with `close()`/`drain()` rather than returning an unowned callback consumer.

Illustrative shape:

```typescript
interface Contract<T> {
  readonly urn: string;
  readonly entityName: string;
  encode(value: T): unknown;
  decode(value: unknown): T;
}

await bus.publish(submitOrder, order, { signal });
const subscription = await bus.consume("orders-ts", submitOrder, async context => {
  await context.publish(orderSubmitted, {
    orderId: context.message.orderId,
    replica: "typescript"
  });
});
```

Do not target browsers in the first implementation. Direct AMQP connectivity, credentials, long-lived consumers, and topology belong in a server runtime such as Node.js.

### Go

- Take `context.Context` as the first operation parameter for cancellation and deadlines.
- Return ordinary `error` values and use wrapping so callers can inspect both a MyServiceBus category and the underlying AMQP error.
- Use generic top-level functions or generic contract-bound types; Go methods cannot introduce their own type parameters.
- Represent handlers as functions returning `error`. A nil error permits acknowledgement after all awaited work completes.
- Make `Close` and `Drain` explicit and idempotent.

Illustrative shape:

```go
type Contract[T any] struct {
    URN        string
    EntityName string
    Encode     func(T) ([]byte, error)
    Decode     func([]byte) (T, error)
}

err := msb.Publish(ctx, bus, SubmitOrderContract, order)
sub, err := msb.Consume(ctx, bus, "orders-go", SubmitOrderContract,
    func(ctx context.Context, message msb.ConsumeContext[SubmitOrder]) error {
        event := OrderSubmitted{OrderID: message.Message.OrderID, Replica: "go"}
        return msb.Publish(ctx, message.Bus, OrderSubmittedContract, event)
    })
```

Do not store an operation context in the bus object. Go's conventions require callers to pass it per operation.

### Rust

- Use `serde::Serialize` and `DeserializeOwned` at the codec boundary, behind a MyServiceBus `Contract<T>` descriptor.
- Return a non-exhaustive MyServiceBus error enum containing underlying sources.
- Represent response alternatives and failures with `Result` and enums rather than exceptions.
- Keep the core API independent of an application web framework. A selected AMQP client may imply Tokio; isolate that choice in the transport crate if practical.
- Tie acknowledgement to an explicit delivery/context owner. Dropping a context must not accidentally acknowledge an unfinished delivery.
- Use cancellation tokens or task cancellation supplied by the selected async runtime, while keeping shutdown/drain explicit.

Illustrative shape:

```rust
bus.publish(&context, &SUBMIT_ORDER, &order).await?;

let subscription = bus
    .consume(&context, "orders-rust", &SUBMIT_ORDER, |delivery| async move {
        delivery
            .publish(&ORDER_SUBMITTED, &OrderSubmitted {
                order_id: delivery.message.order_id,
                replica: "rust".into(),
            })
            .await
    })
    .await?;
```

Rust should follow rather than lead the first spike unless Rust adoption is the immediate product objective.

### Python

- Use `asyncio` coroutines and asynchronous context managers for connection, bus, and subscription ownership.
- Propagate `asyncio.CancelledError` after cleanup; do not translate cancellation into a generic handler failure.
- Use `dataclass` or ordinary typed models in the core examples. Runtime-validation libraries can adapt through the contract codec rather than becoming mandatory.
- Define `Contract[T]` with explicit identity and decoder functions. Never derive the URN from `__module__` or `__name__`.
- Raise category-specific exceptions with chained causes.

Illustrative shape:

```python
async with Bus.connect(url) as bus:
    await bus.publish(SUBMIT_ORDER, order)

    async with bus.consume("orders-python", SUBMIT_ORDER, handle_order):
        await stopped.wait()

async def handle_order(context: ConsumeContext[SubmitOrder]) -> None:
    await context.publish(
        ORDER_SUBMITTED,
        OrderSubmitted(context.message.order_id, "python"),
    )
```

`aio-pika` is the more natural experiment for this shape; Pika remains a valid lower-level alternative if minimizing dependencies is more important than an asyncio-native API.

## Required repository preparation

The current fixtures prove that C# and Java agree, but a third language exposes several assumptions that should become explicit artifacts.

### Contract manifest

Add a versioned manifest containing, for every conformance contract:

- logical name;
- message URN;
- default publish entity;
- implemented-contract URNs in order;
- JSON payload schema or fixture set;
- request/response/fault relationship; and
- examples with canonical values.

This manifest should be used by tests in all languages. It may later drive generated constants and codecs, but generation is not required for the first peer.

### Envelope schema and field rules

Add a normative protocol-v1 JSON Schema or equivalent machine-readable definition for the envelope and fault payload. Document separately the rules JSON Schema cannot express cleanly, including conversation propagation, message URN formatting, address meaning, and the distinction between envelope and native transport metadata.

The schema should define whether missing and explicit-null optional fields are equivalent. Current writers and fixtures should be treated as evidence, not left as the only specification.

### Transport-profile fixture

Add a RabbitMQ profile manifest or test fixture describing:

- exchange and queue names/types;
- durability, exclusivity, and auto-delete flags;
- routing keys and bindings;
- delivery mode and native content type;
- native property mappings;
- publisher confirm and mandatory-routing expectations;
- acknowledgement and failure settlement; and
- temporary response topology.

This prevents each client from reverse-engineering topology from C# or Java source.

### Language-neutral peer protocol

Replace language-specific process-launch assumptions with a tiny peer command contract. A peer should accept commands such as:

```text
fixture-read
publish <contract> <value>
send <endpoint> <contract> <value>
consume <endpoint> <contract> <expected-value>
respond <endpoint> <request-contract> <response-contract> <expected-value>
request <request-contract> <response-contract> <value>
```

It should write line-delimited status events such as `READY`, `SENT`, `RECEIVED`, `RESPONDED`, `FAULT`, and a structured failure event. Broker credentials and dynamically mapped endpoints remain environment variables supplied by the test host.

The protocol is a test-driver contract, not the public client API.

## Minimal conformance matrix

### M0–M1: codec

1. Read the message, request, and fault fixtures without rejecting unknown fields.
2. Validate all UUIDs, timestamps, addresses, message types, headers, payloads, and nulls.
3. Emit a sample envelope that C# and Java deserialize.
4. Read envelopes emitted independently by C# and Java.
5. Reject an invalid UUID, missing required message, unknown contract, and malformed payload with classified errors.

### M2: exchange participant

1. New language publishes `SubmitOrder`; C# and Java consumers each receive it in separate scenarios.
2. C# and Java publish `SubmitOrder`; the new-language consumer receives each.
3. New language sends `SubmitOrder` to a named C# and Java endpoint.
4. C# and Java send `SubmitOrder` to the new-language endpoint.
5. New-language `SubmitOrder` consumer publishes `OrderSubmitted`; a reference client observes the same `orderId`, the expected `replica`, and correct conversation metadata.
6. Handler completion precedes acknowledgement.
7. Cancellation stops intake, waits for a released in-flight handler, and then closes cleanly.
8. An unroutable mandatory send and a failed publisher confirmation surface as failures.

### M2 responder

1. C# request → new-language response.
2. Java request → new-language response.
3. Verify request identifier, conversation identifier, response address, initiator rules, response URN, and cleanup performed by the caller.

### M3: conversation participant

1. New-language request → C# response and Java response.
2. New-language request → C# fault and Java fault.
3. C# and Java request → new-language fault.
4. Timeout cancels local waiting, deletes temporary topology, and ignores a late response.
5. Concurrent requests for the same response type remain isolated by request identifier.

MassTransit directions should be added after the corresponding MyServiceBus C#/Java directions pass. This keeps protocol debugging separate from third-party compatibility debugging.

## Recommended implementation sequence

### Phase 0: make the protocol implementable

1. Add the contract manifest, envelope schema, and RabbitMQ profile fixture.
2. Extract a reusable cross-language peer command contract from the Java peer.
3. Add golden payloads for `SubmitOrder`, `OrderSubmitted`, `TestRequest`, `TestResponse`, and `Fault<TestRequest>`.
4. Make the .NET conformance host parameterize a peer executable and arguments rather than assuming Gradle/Java.

### Phase 1: Go thin peer

1. Create a private test peer using `amqp091-go`; do not create a published module yet.
2. Implement explicit contracts and the protocol-v1 envelope codec.
3. Pass M0 and M1.
4. Add publish, send, consume, manual acknowledgement, and graceful drain against pre-provisioned topology.
5. Pass M2 in both C# and Java directions.
6. Add response support and pass the M2 responder matrix.

At this point the project has validated a third implementation model without committing to a supported package.

### Phase 2: decide product direction

Evaluate:

- demonstrated user demand for Go versus TypeScript, Python, or Rust;
- API quality and amount of duplicated transport code;
- dependency maintenance and security posture;
- packaging/release cost;
- CI time for the expanded cross-language matrix; and
- whether the thin participant should remain a conformance peer, become a supported transport package, or serve as a template for community clients.

Only after this decision should the new client receive public package workflows, release artifacts, package-smoke consumers, and supported-version claims.

### Phase 3: expand deliberately

Add M3 requests/faults, then M4 RabbitMQ operational semantics. Azure Service Bus is a separate transport-profile decision and must not be inferred from RabbitMQ success. Higher-level APIs such as filters, DI integration, outbox, scheduling, and monitoring should be demand-driven and independently specified.

## What not to implement initially

The first peer does not need:

- a custom dependency-injection container;
- reflection or package scanning;
- consumer classes or annotations/decorators;
- generated dispatch;
- middleware/filter pipelines;
- retries or delayed redelivery;
- scheduling;
- outbox/inbox persistence;
- anonymous/interface message materialization;
- multiple serializers;
- topology inspection;
- monitoring/dashboard export;
- native compilation support;
- multiple transports; or
- API similarity to MassTransit.

These are product features, not prerequisites for proving that another language can participate in the message model.

## Decision gates

Promote a thin peer to a published client only when:

- its supported maturity stage and compatibility evidence are explicit;
- every claimed operation passes in both C# and Java directions;
- contract identity never depends on local module/package naming;
- unknown fields and newer contract-manifest entries are handled compatibly;
- publisher acceptance and delivery settlement cannot report false success;
- cancellation and graceful shutdown have bounded tests;
- package ownership, version support, dependency updates, and security response are assigned;
- the release workflows and staged package consumers include the new artifact; and
- documentation distinguishes missing features from deliberate language idioms.

## References

Repository sources:

- [MyServiceBus Specification](../specs/myservicebus-spec.md)
- [Transport Specification](../specs/transport-spec.md)
- [Compatibility Policy](../compatibility.md)
- [RabbitMQ Transport](../rabbitmq-transport.md)
- [Testing](../testing.md)
- [`test/fixtures/protocol/v1`](../../test/fixtures/protocol/v1)

External primary sources:

- [RabbitMQ AMQP 0-9-1 tutorials](https://www.rabbitmq.com/tutorials)
- [AMQP 0-9-1 specification](https://www.rabbitmq.com/resources/specs/amqp0-9-1.pdf)
- [RabbitMQ JavaScript tutorial using amqplib](https://www.rabbitmq.com/tutorials/tutorial-one-javascript)
- [amqplib documentation](https://amqp-node.github.io/amqplib/)
- [RabbitMQ Go tutorial using amqp091-go](https://www.rabbitmq.com/tutorials/tutorial-one-go)
- [RabbitMQ-maintained amqp091-go](https://github.com/rabbitmq/amqp091-go)
- [RabbitMQ-listed Rust clients](https://www.rabbitmq.com/tutorials)
- [amqprs API documentation](https://docs.rs/amqprs/latest/amqprs/)
- [RabbitMQ Python tutorial using Pika](https://www.rabbitmq.com/tutorials/tutorial-one-python)
- [aio-pika repository and documentation](https://github.com/mosquito/aio-pika)
- [Go context package](https://go.dev/pkg/context/)
- [Python asyncio task cancellation](https://docs.python.org/3/library/asyncio-task.html#task-cancellation)
