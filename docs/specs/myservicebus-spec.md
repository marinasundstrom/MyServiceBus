# MyServiceBus Specification

## Purpose and Maturity

MyServiceBus is being developed as a language-neutral messaging specification with C# and Java reference implementations. The specification describes the MyServiceBus abstraction—its concepts, primitives, relationships, and observable behavior—without prescribing one language's classes, one runtime's internal structure, or the details of an individual transport.

This is currently a **living, implementation-informed specification**. The C# and Java implementations are used to discover missing concepts, test whether an abstraction works naturally in more than one language, and provide evidence for behavioral rules. Some sections describe stable shared behavior; others record an emerging model that may be refined while the project is pre-1.0.

The intended future state is that another language client can be designed primarily from this specification, its profiles, canonical fixtures, and conformance scenarios. It should not require translating either reference implementation or reverse-engineering behavior from its tests.

Compatibility claims are scoped by level, client version, and transport profile. See the [Compatibility Policy](../compatibility.md). The immediate target is verified wire and semantic compatibility plus named transport-profile interoperability across the C# and Java clients and supported MassTransit versions.

## How the Specification Evolves

The project develops the specification and reference implementations together:

1. A capability begins as an **exploratory concept**. Design may be informed by one implementation, prototypes, and platform constraints.
2. It becomes a **portable concept** when its responsibility and expected outcomes can be explained without C#- or Java-specific types.
3. It becomes **specified behavior** when observable success, failure, cancellation, identity, and lifecycle rules are written down.
4. It becomes a **conformance target** when shared fixtures or scenarios can verify it independently in each implementation.
5. It becomes a **compatibility commitment** only under the versioning and readiness rules of the compatibility policy.

Implementation behavior is evidence during this process, not automatically a portable requirement. Conversely, emerging prose is not automatically a stable promise. When prose, fixtures, and reference implementations disagree, the project must resolve the discrepancy explicitly and record whether the specification, the implementation, or both change.

## Specification Structure

MyServiceBus separates portable meaning from the mechanisms used to realize it.

| Layer | Describes | Examples |
| --- | --- | --- |
| Conceptual model | Language-neutral things and their relationships | Contract, message, endpoint, consumer, context |
| Portable behavior | Outcomes visible to applications | Send, publish, consume, request, retry, fault |
| Protocol profile | Wire representation and propagation rules | Envelope, message URNs, headers, correlation |
| Transport abstraction | The boundary through which messages are moved | Capabilities, delivery, settlement, address resolution |
| Feature profile | Optional portable capabilities | Outbox/inbox, topology inspection, scheduling |
| Implementation mapping | Non-normative realization on one platform | C#, Java, or a future client runtime |
| Conformance | Evidence that a declared profile is implemented | Canonical fixtures, scenario and interoperability tests |

Individual transport profiles sit below this specification. They define how a transport realizes addresses, topology, acceptance, settlement, native metadata, temporary endpoints, and failure preservation. Those details do not become part of the MyServiceBus abstraction. An implementation may use different names, native types, construction patterns, and asynchronous APIs while preserving the portable concepts and observable outcomes it claims to support. Public interface shape is also outside the core specification.

## Conceptual Model

### Contract and message

A **contract** is the language-neutral identity and shape under which data is exchanged. An implementation may associate a native type with a contract, but portable contract identity does not rely on process-local type identity alone.

A **message** is one occurrence of a contract and its application data. An outgoing occurrence has a message identity that remains stable across in-process retry, transport redelivery, and compatibility copies. Creating a new outgoing message normally creates a new identity.

A concrete message may advertise several eligible contracts, such as implemented interfaces or non-root base contracts. The selected protocol profile defines their identity and ordering.

### Envelope

An **envelope** carries a message plus transport-independent exchange metadata. Depending on the operation and profile, it contains:

- message, request, correlation, conversation, and initiator identities;
- source, destination, response, and fault addresses;
- sent time and expiration;
- advertised contract identities;
- application headers;
- content type and serialized message data; and
- optional host and diagnostic metadata.

The envelope is a protocol value, not a serialized language-specific context object. Its concrete field names, encoding, content type, and compatibility rules belong to a protocol profile rather than to the transport-independent abstraction.

### Address and endpoint

An **address** identifies a source or destination. The portable model treats it as an opaque transport-owned value and distinguishes logical endpoint intent from its transport-specific representation.

An **endpoint** is a sending or receiving boundary with configuration, topology intent, and operation pipelines. A **receive endpoint** selects consumers for delivered contracts and owns the outcome of the source delivery. A **send endpoint** accepts directed messages for one destination.

### Consumer and context

A **consumer** is application behavior registered for one or more contracts. A **consume context** carries the delivered message, envelope metadata, headers, cancellation, and operations derived from that delivery.

Context is explicit. Thread-local, task-local, or process-global state is not the authoritative source of propagation metadata.

When several consumers on one endpoint match one source delivery, they form one delivery outcome. The delivery succeeds only when every selected consumer pipeline succeeds.

### Bus and mediator

A **bus** coordinates configuration, endpoints, topology, transport lifecycle, and broker-backed message operations. The supported hosting model is one logical bus per application.

A **mediator** applies compatible message, consumer, request, response, pipeline, and fault concepts within one process. It does not imply durability, independent delivery, broker settlement, or externally observable publication.

### Pipe and filter

A **context** flows through a **pipe**, which is an ordered composition of **filters**. Filters may observe, enrich, short-circuit, retry, or fail an operation. Ordering, wrapping, failure propagation, retry re-entry, lifetime, and cancellation are defined in the [Pipeline and Filter Specification](pipeline-filter-spec.md).

### Transport

A **transport** moves encoded messages between endpoints and reports outcomes to the runtime. The abstraction requires it to resolve addresses, produce send and receive boundaries, expose capabilities, participate in lifecycle, and implement delivery and settlement outcomes without leaking transport-native objects into portable application behavior.

Every transport describes known capabilities as `native`, `emulated`, or `unsupported`. Configuration that requires unsupported behavior is rejected rather than silently weakened.

The separate [Transport Specification](transport-spec.md) defines the adapter contract and how named transport profiles refine it. All concrete transport profiles remain outside this core specification.

## Portable Operations

The notation below is conceptual. It is not a required method signature.

### Send

`send(destination, contract, message, options)` expresses directed delivery.

- The destination is resolved through the transport abstraction.
- A send context passes through the send pipeline before serialization and transport dispatch.
- Successful completion means the transport reached its declared acceptance boundary; the core does not define the mechanism used to establish that outcome.
- Failure, cancellation, rejection, and an ambiguous outcome remain distinguishable where the transport can observe them.

### Publish

`publish(contract, message, options)` expresses delivery to subscriptions eligible for the advertised contract set.

- The publish pipeline completes before the send pipeline of the resolved transport endpoint.
- A consumer is invoked at most once for one source delivery even when several advertised contracts match it.
- Zero current subscribers is a valid portable publish outcome.
- Successful publish means transport acceptance, not proof that a consumer processed the message.

### Consume

`consume(endpoint, contract, handler)` associates application behavior with a receiving boundary.

- The message is deserialized and validated before consumer dispatch.
- Successful application completion permits successful source settlement.
- A process failure before settlement does not turn incomplete work into a successful delivery. A durable transport makes the unsettled delivery eligible for another attempt according to its profile.
- Broker-backed consumers assume at-least-once processing. Exactly-once application effects require a separately specified transactional or idempotency boundary.

### Request and response

A **request** composes directed send with a request identity, response address, local deadline, and matching response or fault consumption.

- Concurrent requests are matched by request identity, not only by response type.
- A response retains the request identity and is sent to the response address.
- A fault uses the fault address when present and otherwise the response address.
- Caller-supplied correlation remains visible to the consumer but is not automatically copied to the response correlation identity.
- Timeout or cancellation ends the caller's wait; it does not prove that remote work was cancelled.

A temporary endpoint is one possible implementation, not the definition of request/response.

### Conversation and causation

A new top-level outgoing operation starts a conversation when none was supplied. An outgoing operation initiated while consuming retains the consumed conversation. A consumed correlation identity becomes the initiator identity of the new message; it is not implicitly reused as the new correlation identity.

These rules apply to consumer-initiated send, publish, response, and fault operations.

### Retry, redelivery, fault, and error

An **in-process retry** re-enters a downstream portion of a pipeline without settling and reacquiring the source. A **redelivery** is another transport delivery attempt. They are separate concepts and capabilities.

- Retry is opt-in and re-executes application code; it does not roll back earlier side effects.
- Message identity remains stable across retry and redelivery.
- After retries are exhausted, the final application failure becomes the terminal failure.
- A terminal failure produces a fault notification when fault behavior is enabled and invokes the configured failed-delivery policy.
- A failed or skipped source is not reported as successfully consumed until required preservation succeeds. The transport profile defines how preservation and later delivery attempts are realized.

Acceptance, settlement, duplication, ordering, crash windows, and shutdown are refined in the [Delivery Guarantees Specification](delivery-guarantees.md).

### Cancellation and lifecycle

Every asynchronous operation context carries a cancellation signal. Cancellation is cooperative and uses the implementation's native or idiomatic representation. It stops waiting where supported; it does not prove that a transport operation already in flight was rejected.

Startup validates configuration and capability requirements before receive endpoints accept deliveries. Graceful shutdown stops new delivery and gives active work a bounded opportunity to complete. Timed-out or forced shutdown does not acknowledge incomplete application work as successful.

## Topology

The configured system has one normalized topology model containing contracts, endpoints, consumers, and bindings. It describes portable application intent. A transport projects that intent into its own resources and validates its constraints.

Transport details are additive projection data and do not redefine portable identity. Inspection and operational tooling query the normalized model rather than reconstructing it from transport-specific resources. See the [Topology Model Specification](topology-model-spec.md).

## Optional Feature Profiles

An implementation claims optional profiles separately from the portable core:

- [Transactional Outbox and Inbox](outbox-inbox.md) defines transactional intent, persistent identity, leasing, deduplication, and broker settlement boundaries.
- [Topology Extension Model](topology-extension-model.md) defines how transports and higher-level features add facts without replacing portable topology.
- Scheduling, recurring jobs, monitoring, inspection, and testing remain documented capability areas and may graduate into more formal profiles as their models stabilize.

An optional profile should state its prerequisites, observable behavior, failure boundary, capability declaration, and conformance evidence. Similar classes in both reference implementations are useful evidence but do not by themselves define a portable feature.

## Implementations

The core specification does not prescribe interfaces, class hierarchies, method overloads, packages, dependency-injection frameworks, or asynchronous result types. An implementation chooses idiomatic platform mechanisms and declares the protocol, transport, and optional feature profiles it supports.

The current [C# implementation notes](csharp-client-spec.md) and [Java implementation notes](java-client-spec.md) explain how the reference clients realize the model. They are examples and status records, not additional sources of portable concepts or required API shapes.

## Conformance and Future Implementations

Conformance is evaluated against a declared set of profiles and versions, not by source similarity to C# or Java. As the specification matures, an implementation should be able to demonstrate support by:

1. declaring its protocol profiles, transport profiles, and optional features;
2. reading and writing canonical protocol fixtures;
3. passing shared behavioral scenarios for each claimed operation;
4. validating unsupported transport capabilities before startup;
5. preserving specified identities and outcomes across retry, redelivery, failure, and cancellation; and
6. passing named interoperability scenarios for external compatibility claims.

The shared fixtures and scenarios are the executable portion of the specification. Reference-client tests that depend on private C# or Java structure are implementation tests and are not requirements for a new client.

The [Minimum Viable Language Client](../development/minimum-viable-language-client.md) turns this model into an incremental implementation path. The [porting checklist](../development/porting-checklist.md) should be used to verify that a new binding follows specified behavior rather than mechanically translating an existing codebase.

## Relation to MassTransit

MyServiceBus adopts MassTransit concepts and wire behavior where they provide interoperability, migration value, or a useful shared model. The compatibility profile does not require source compatibility or the complete MassTransit feature set. See [Differences from MassTransit](../masstransit-differences.md) for deliberate deviations.

## Deliberate Non-Requirements

The base specification does not require:

- identical APIs or internal architecture across languages;
- concrete address syntax, topology resources, native metadata, or settlement mechanisms for an individual transport;
- every optional feature in every client;
- exactly-once broker delivery or application effects;
- one abstraction that erases the differences between queues, streams, webhooks, and realtime sessions;
- a particular dependency-injection, logging, tracing, or serializer library; or
- multiple hosted buses in one application.

These boundaries allow MyServiceBus to grow from two aligned implementations into a specification that future implementations can follow.
