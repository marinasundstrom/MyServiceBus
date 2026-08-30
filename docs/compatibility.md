# Compatibility Policy

## Purpose

Compatibility in MyServiceBus is a set of independently testable promises. It does not mean reimplementing every MassTransit feature or reproducing its complete public API.

Every compatibility claim must identify:

- the compatibility level
- the MyServiceBus client and version
- the transport profile
- the tested MassTransit version or version range, when applicable
- any capability constraints or documented differences

## Compatibility Priorities and Deliberate Divergence

Compatibility is prioritized in this order:

1. MyServiceBus clients must share a stable, language-neutral protocol and portable semantics.
2. Supported transport profiles must interoperate with MassTransit where that enables mixed deployments and migration.
3. C# APIs should remain familiar to MassTransit users, while every language exposes the same concepts idiomatically.
4. Historical MassTransit behavior is optional when it does not contribute to interoperability, migration, or current user value.

MyServiceBus may deliberately diverge from legacy MassTransit edges, but a divergence must be explicit. Its rationale, affected compatibility level, replacement behavior, and migration impact must be documented. Conformance tests must distinguish intentional differences from regressions. After a stable protocol policy is declared, wire-format or transport-profile divergence requires a versioned protocol decision and must not silently break supported peers.

### Alignment-Phase Directive

Until MyServiceBus explicitly declares a stable protocol compatibility policy, MassTransit is authoritative for every wire shape and transport convention that MyServiceBus claims as MassTransit-compatible. When existing MyServiceBus behavior conflicts with that target, implementations, fixtures, and tests must be corrected to match MassTransit. The project does not need to retain aliases, fallback parsing, or compatibility modes solely for earlier MyServiceBus behavior during this alignment phase.

This directive applies to accidental incompatibilities, not deliberate product divergence. A deliberate divergence remains allowed under the policy above, but it must be named and must not be presented as MassTransit-compatible behavior.

## Compatibility Levels

### Level 1: Wire Compatibility

Two implementations can exchange serialized messages without a custom translation layer.

This level covers:

- the MassTransit JSON envelope and content type
- message type URNs
- message, correlation, conversation, initiator, request, and response identifiers
- source, destination, response, and fault addresses
- headers and host metadata
- request, response, and `Fault<T>` envelope shapes
- serialization rules for supported contract types

Wire compatibility does not by itself guarantee that a broker routes the message to the intended consumer.

### Level 2: Semantic Compatibility

The implementations give the same application-level meaning to the portable messaging operations.

This level covers:

- directed send
- publish/subscribe
- consume and successful settlement
- request/response and fault responses
- retry and terminal failure behavior
- skipped-message and error handling
- correlation propagation
- cancellation and lifecycle behavior where the language and transport allow it

Semantic compatibility is bounded by declared transport capabilities. An unsupported semantic must be rejected or documented; it must not be silently weakened.

### Level 3: Transport-Profile Interoperability

MyServiceBus and MassTransit can communicate directly through a named transport using compatible addressing, topology, native properties, and settlement behavior.

Each profile defines:

- URI schemes and address normalization
- queue, exchange, topic, and subscription naming
- send and publish topology
- native-header mapping
- acknowledgement, rejection, and redelivery
- error, skipped, and fault destinations
- temporary endpoint and request/response conventions
- supported, emulated, and unsupported capabilities

Compatibility at this level is specific. Passing the RabbitMQ profile does not imply Azure Service Bus, SQS/SNS, Kafka, or any other profile.

### Level 4: API and Concept Familiarity

Developers can transfer their MassTransit knowledge to MyServiceBus.

For C#, the public API should remain recognizably MassTransit-like where that improves adoption: consumers, contexts, endpoints, configuration, filters, send, publish, and request clients should feel familiar.

This is not a source-compatibility promise. Applications are not expected to replace a package reference and compile unchanged.

For Java and future languages, concept familiarity is the target. APIs should preserve the same behavior while following the host language's conventions for builders, dependency injection, concurrency, cancellation, and errors.

### Level 5: Cross-Language Parity

MyServiceBus clients in different languages provide the same portable behavior and can interoperate through a shared transport profile.

Parity includes:

- equivalent wire representations
- equivalent portable messaging behavior
- equivalent configuration outcomes
- compatible inspection and monitoring DTOs
- shared conformance scenarios

Parity does not require identical method names, type systems, dependency injection frameworks, or asynchronous programming models.

## Verified Compatibility Target

The verified target is the **RabbitMQ and Azure Service Bus interoperability
baseline for the C# and Java reference clients**.

The target consists of:

1. **Level 1 wire compatibility** between MyServiceBus C#, MyServiceBus Java, and supported MassTransit versions.
2. **Level 2 semantic compatibility** for the portable core: send, publish, consume, request/response, correlation, retry, faults, skipped messages, and error handling.
3. **Level 3 RabbitMQ and Azure Service Bus transport-profile interoperability** in both directions between MyServiceBus and MassTransit.
4. **Level 4 API familiarity** for C# and concept familiarity with idiomatic APIs for Java.
5. **Level 5 C#↔Java parity** for the portable core and both broker profiles.

This verified RabbitMQ baseline establishes the rule for every broker-backed
transport that MyServiceBus promotes to supported status. Its C# and Java
clients must use the same transport-profile naming and topology conventions as
the supported MassTransit peer so that services can communicate without a
MyServiceBus-specific bridge. Each transport advances independently and must
prove both MyServiceBus-to-MassTransit and MassTransit-to-MyServiceBus flows;
compatibility on one transport is never evidence for another.

The immediate target explicitly does not include:

- source compatibility with MassTransit
- every MassTransit consumer, saga, routing-slip, persistence, scheduler, or middleware API
- transport-profile compatibility beyond RabbitMQ and Azure Service Bus
- treating Kafka, SignalR, or serverless hosts as interchangeable queue transports
- multiple bus instances in one host or MassTransit's marker-interface registration model
- complete feature parity with the latest MassTransit release

Amazon SQS/SNS is an experimental preview profile. Matching C# and Java adapters pass directed SQS and raw SNS-to-SQS publication scenarios in both directions with MassTransit 8.5.1 against the checked-in LocalStack fixture. LocalStack is the default acceptance environment; the manual AWS gate is reserved for documented emulator differences, observed discrepancies, and AWS-only concerns such as IAM. Portable pipeline behavior remains covered by the shared suites. FIFO queues and FIFO-specific semantics are not part of this MVP profile.

## Immediate Conformance Matrix

The following scenarios should become required integration tests:

| Producer | Consumer | Required scenarios |
| --- | --- | --- |
| MyServiceBus C# | MyServiceBus Java | Send, publish, request/response, fault |
| MyServiceBus Java | MyServiceBus C# | Send, publish, request/response, fault |
| MyServiceBus C# | MassTransit | Send, publish, request/response, fault |
| MassTransit | MyServiceBus C# | Send, publish, request/response, fault |
| MyServiceBus Java | MassTransit | Send, publish, request/response, fault |
| MassTransit | MyServiceBus Java | Send, publish, request/response, fault |

Each direction should verify envelope fields, message URNs, native RabbitMQ properties, topology, settlement, retries, skipped-message routing, and terminal error behavior where applicable.

## Current Baseline

The repository currently has the following executable foundation:

| Check | C# | Java | Status |
| --- | --- | --- | --- |
| Read the shared message envelope fixture | Implemented | Implemented | Verified locally |
| Read the shared request envelope fixture | Implemented | Implemented | Verified locally |
| Read the shared fault envelope fixture | Implemented | Implemented | Verified locally |
| Round-trip a compatible envelope through RabbitMQ | Testcontainers | Testcontainers | Verified independently per client |
| C# producer → Java consumer | Implemented | Implemented | Verified locally through RabbitMQ |
| Java producer → C# consumer | Implemented | Implemented | Verified locally through RabbitMQ |
| MyServiceBus → MassTransit | Verified from C# | Verified from Java | Verified locally through RabbitMQ |
| MyServiceBus directed send → MassTransit queue | Verified from C# | Verified from Java | Queue-address delivery verified through RabbitMQ |
| MassTransit directed send → MyServiceBus queue | Verified with C# | Verified with Java | Queue-address delivery verified through RabbitMQ |
| C# ↔ Java directed send | Verified producer and consumer | Verified producer and consumer | Both queue-address directions verified through RabbitMQ |
| MassTransit → MyServiceBus | Verified with C# | Verified with Java | Verified locally through RabbitMQ |
| MyServiceBus request → MassTransit response | Verified from C# | Verified from Java | Correlated request/response verified through RabbitMQ |
| MassTransit request → MyServiceBus response | Verified with C# | Verified with Java | Correlated request/response verified through RabbitMQ |
| MyServiceBus request → MassTransit fault | Verified from C# | Verified from Java | Correlated fault response verified through RabbitMQ |
| MassTransit request → MyServiceBus fault | Verified with C# | Verified with Java | Correlated fault response verified through RabbitMQ |
| MassTransit message → MyServiceBus `_skipped` | Verified with C# | Verified with Java | Unknown message is preserved and remains consumable as a MassTransit envelope |
| Retry exhaustion → MyServiceBus `_error` | Verified with C# | Verified with Java | `Immediate(2)` performs three total attempts before preserving the message in `_error` |

The shared versioned fixtures live under `test/fixtures/protocol/v1`. They are canonical inputs for MyServiceBus protocol tests, but they do not become evidence of MassTransit interoperability until the corresponding MassTransit scenarios pass.

RabbitMQ transport integration tests use a pinned RabbitMQ image through Testcontainers. They must use the container's dynamically mapped host and AMQP port and must not depend on a broker installed on the developer machine or a fixed host port.

The cross-language tests are opt-in during ordinary local test runs because they start both runtimes. CI runs them in a dedicated interoperability job. Set `RUN_CROSS_LANGUAGE_TESTS=1` to execute them locally.

The current RabbitMQ baseline uses RabbitMQ `4.1.8-alpine` and MassTransit `8.5.1`. Verification covers compatible envelope publication, directed send in every C#, Java, and MassTransit direction, consumption, correlated request/response, correlated fault responses, retry exhaustion, and MassTransit-readable `_error` and `_skipped` delivery. The complete runtime, tooling, client-library, and preview support contract is recorded in [Supported Versions](supported-versions.md).

This baseline is **verified with documented limitations** for the scenarios in the matrix. It is not a claim of complete MassTransit feature or API compatibility.

The Azure Service Bus preview baseline uses a live Standard-tier namespace and
MassTransit `8.5.1`. It verifies corresponding C# and Java Create-mode topology,
default and explicit naming, publish and directed send in both MassTransit
directions, cross-language delivery, correlated responses and faults, lock
renewal, and terminal failure preservation in `_error` followed by source
completion. It is likewise **verified with documented limitations**; sessions,
managed identity, transactions, native scheduling, and the other capabilities
listed as unsupported or unexposed in the transport profile are not implied.

## Compatibility Status Labels

Documentation and package metadata should use consistent labels:

- **Verified**: required automated conformance scenarios pass for the named versions and profile.
- **Compatible with limitations**: core scenarios pass, with explicitly documented unsupported or emulated capabilities.
- **Experimental**: implemented but not yet covered by the complete conformance suite.
- **Concept compatible**: shares the application model but does not claim direct protocol or transport interoperability.
- **Unsupported**: no compatibility promise is made.

Avoid unqualified claims such as “fully MassTransit compatible.” Prefer a statement such as:

> MyServiceBus is verified for the documented MassTransit-compatible RabbitMQ
> and Azure Service Bus send, publish, consume, request/response, fault, and
> terminal-settlement scenarios across its C# and Java clients.

That statement should only be published after the associated version matrix passes.

## Future Targets

After the RabbitMQ and Azure Service Bus baselines:

1. Keep both profiles gated by their declared cross-language and MassTransit
   matrices as their preview APIs evolve.
2. Define a separate event-stream profile before implementing Kafka.
3. Validate the language-neutral specification through a third language client.

Every future transport or language begins as experimental and advances independently through the applicable compatibility levels.
