# MyServiceBus Roadmap

## Purpose

This roadmap turns the project direction into an incremental delivery plan. It is directional rather than a release commitment: phases are ordered by dependency and learning value, while dates should be assigned only when maintainers select work for a release.

MyServiceBus aims to become a modern, cross-language messaging runtime that:

- interoperates with MassTransit through explicit protocol and transport profiles
- provides a small, consistent messaging model across languages
- supports multiple broker families without erasing their differences
- exposes optional inspection and monitoring APIs for operational tools
- supports a read-only dashboard before introducing control-plane operations

The roadmap is centered on replacing MassTransit in basic broker-backed messaging scenarios. It does not currently seek to turn MyServiceBus into a general-purpose publisher/consumer abstraction for technologies without service-bus topology.

It is positioned as a focused alternative for teams that do not need a large enterprise service-bus platform. Interoperability with MassTransit enables mixed systems and migration; it does not turn MassTransit's complete feature catalog into the destination of this roadmap.

## Decision Guardrails

Use these rules when accepting roadmap work:

1. A portable-core feature must be specifiable independently of C# and implementable naturally in multiple languages.
2. MassTransit compatibility work must identify its target level: wire, semantic, transport profile, or API familiarity.
3. A transport must declare unsupported and emulated behavior; it must not silently reduce delivery guarantees.
4. C# and Java changes to shared behavior ship together or create an explicit, temporary parity entry.
5. Inspection and monitoring remain optional addons. Message delivery must not depend on a dashboard or central registry.
6. New language clients begin with conformance fixtures and one supported transport profile, not the full accumulated feature set.
7. A MassTransit feature is not added solely for feature parity. It must materially improve interoperability, migration, or the focused MyServiceBus user experience.
8. Prefer a small, coherent portable core over enterprise breadth; specialized patterns stay demand-driven.
9. Keep shared concepts and useful counterpart types recognizable across clients, but never derive Java packages or APIs mechanically from C# namespaces and language features, or vice versa.
10. Treat the normalized topology query model as a foundational API. Runtime provisioning, inspection, and dashboards must consume it rather than constructing separate topology interpretations.
11. Keep broker-backed service-bus semantics as the stable product boundary. Explore HTTP, webhooks, realtime sessions, and similar delivery mechanisms separately and generalize the core only from demonstrated shared requirements.
12. Treat mediator dispatch as an explicitly local execution mode. Externally observable events normally follow the broker-backed path.
13. Support one logical bus per application. Do not add multiple hosted buses solely for MassTransit compatibility; reconsider them only for a concrete cross-platform use case with an idiomatic Java dependency-injection model.

## Phase 1: Protocol Baseline

**Outcome:** compatibility becomes measurable rather than aspirational.

- Define and document the compatibility levels.
- Version the language-neutral protocol and capability descriptor.
- Create canonical JSON fixtures for envelopes, headers, message URNs, requests, responses, and faults.
- Create C#↔Java interoperability tests for publish, send, request/response, retries, and terminal faults.
- Add MyServiceBus↔MassTransit RabbitMQ scenarios in both directions.
- Run RabbitMQ integration and interoperability scenarios against disposable Testcontainers brokers with dynamically mapped ports.
- Record tested MassTransit versions and intentional differences in a compatibility matrix.
- Update the public specification so implementations are validated against it rather than inferred from one client.

**Exit criteria:** the reference clients pass the same protocol fixtures and the RabbitMQ interoperability matrix runs repeatably in CI.

**Status:** implemented. The versioned fixtures and Testcontainers matrix cover the documented C#, Java, and MassTransit RabbitMQ baseline. Release claims remain scoped to the pinned versions in the compatibility policy.

The precise scope and matrix for this phase are defined in the [Compatibility Policy](compatibility.md).

## Phase 2: Transport Capability Foundation

**Outcome:** additional transports can be added without encoding RabbitMQ assumptions into the portable core.

- Define the `native`, `emulated`, and `unsupported` capability model.
- Separate portable message semantics from transport topology and settlement contracts.
- Define transport-profile requirements for addressing, naming, topology, native headers, scheduling, redelivery, and errors.
- Add startup validation for requested features that a transport cannot provide.
- Refactor the new-transport checklist around capability and conformance tests.
- Decide which stream concepts require distinct producer and endpoint APIs.

**Exit criteria:** RabbitMQ and in-memory adapters describe their capabilities, and invalid feature combinations fail clearly before startup.

**Status:** implemented. Both reference clients expose matching versioned descriptors for RabbitMQ and in-memory transports, validate explicit capability requirements before receive transports start, and route publish, request, temporary, error, and fault address production through transport profiles. The transport specification and new-transport checklist distinguish durable bus transports, event streams, hosting adapters, and application integrations.

## Fundamentals Stability Gate

**Outcome:** higher-level features build on conforming, queryable, and intentionally versioned fundamentals.

- Define the normalized topology model and corresponding idiomatic C# and Java query APIs.
- Separate mutable registration state and runtime callbacks from stable topology snapshots.
- Add canonical cross-language topology fixtures and conformance tests.
- Replace RabbitMQ-shaped portable receive-topology fields with endpoint intent plus transport projections.
- Define stability and evolution rules for protocol, topology, transport capabilities, lifecycle, and failure semantics.
- Validate the extension model against prospective saga, outbox, and second-transport requirements without implementing those features prematurely.

**Exit criteria:** equivalent C# and Java configurations produce the same canonical topology snapshot; RabbitMQ provisioning consumes a profile projection of that model; inspection can query it without inventing broker defaults; and the foundational compatibility contracts have an explicit versioning policy.

The [Topology Model Specification](specs/topology-model-spec.md) defines the target boundary. This gate precedes expansion of inspection, dashboard, saga, outbox, and additional transport work.

The [MVP Release Gate](development/mvp-release-gate.md) defines the release boundary and the remaining packaging, documentation, and release-candidate work that follows this fundamentals gate.

**Status:** implemented. The normalized query APIs, version 1 canonical fixture, receive-endpoint intent, inspection consumption, synchronized snapshot-version constants, profile-neutral runtime endpoint topology, and named RabbitMQ receive-topology projection are implemented in C# and Java. Legacy transport overloads remain compatibility adapters. The [Topology Extension Model](specs/topology-extension-model.md) validates additive saga and outbox nodes plus a materially different Azure Service Bus projection without prematurely implementing those features.

## Mediator and In-Memory Stability Gate

**Outcome:** local dispatch and testing are predictable, cross-language implementations of the same application-visible messaging fundamentals.

- Separate mediator runtime responsibilities from test-harness observation responsibilities.
- Define matching C# and Java conformance scenarios for lifecycle, send, publish, request/response, faults, retries, filters, scopes, headers, cancellation, telemetry, scheduling, concurrency, and topology queries.
- State which MassTransit mediator and in-memory semantics are compatible, intentionally different, or unsupported.
- Make timing and failure guarantees deterministic enough for application tests.
- Stabilize the ordinary mediator and harness APIs before adding another broker transport.

**Exit criteria:** both reference clients pass the shared scenario matrix, capability descriptors match real behavior, and the documented lifecycle and delivery guarantees are suitable for preview packages.

The detailed checklist is defined in the [Mediator and In-Memory Stability Gate](development/in-memory-stability-gate.md).

## Phase 3: Inspection and Monitoring APIs

**Outcome:** applications and tools can discover and observe a running MyServiceBus instance without coupling the core to a UI.

- Complete first-party inspection addons for C# and Java.
- Stabilize language-neutral DTOs for services, instances, endpoints, consumers, contracts, versions, and capabilities.
- Implement the monitoring addon described in the monitoring proposal.
- Capture aggregate runtime state and high-signal records for retries, faults, skipped messages, and moves to error.
- Keep health endpoints and OpenTelemetry integrations distinct but discoverable.
- Define optional registration or heartbeat events for aggregating multiple runtime instances.

**Exit criteria:** equivalent C# and Java services expose compatible inspection and monitoring snapshots, and consumers can use the programmatic APIs without HTTP.

## Phase 4: Read-Only Dashboard

**Outcome:** operators can understand topology and runtime behavior across services.

- Build the dashboard solely against stable inspection, monitoring, health, and telemetry interfaces.
- Show services, instances, endpoints, consumers, contracts, and producer/consumer relationships.
- Show compatibility and capability warnings.
- Show retries, faults, skipped messages, error moves, and links to traces.
- Add an optional registry/collector for multi-instance discovery and historical monitoring.
- Define optional broker-metrics adapters for queue depth and broker-native health.

**Exit criteria:** the dashboard can visualize a mixed C#/Java system and remains useful when broker administration APIs are unavailable.

## Phase 5: Second Durable Broker Profile

**Outcome:** the transport model is proven against a managed broker with materially different semantics.

Azure Service Bus is the recommended first candidate because topics, subscriptions, sessions, native dead-lettering, and scheduling exercise the capability model. Final selection should follow demonstrated user demand and maintainer access.

- Implement the adapter in C# and Java, or document a deliberately staged parity plan.
- Define the MassTransit-compatible address and topology profile.
- Add broker-specific capability constraints and configuration.
- Add cross-language and MassTransit interoperability tests.
- Document migration limits between RabbitMQ and the new transport.

**Exit criteria:** the new adapter passes portable conformance tests and its own transport-profile interoperability suite.

## Phase 6: Event-Stream Profile

**Outcome:** Kafka or another event stream is supported without pretending it is a queue broker.

- Define topic producers, topic endpoints, keys, partitions, offsets, checkpoints, and consumer groups.
- Decide which envelope and fault conventions are shared with bus transports.
- Specify ordering, replay, delayed redelivery, and error-topic behavior explicitly.
- Implement Kafka as the initial stream adapter when justified by users.
- Add stream-specific inspection and monitoring fields as additive transport details.

**Exit criteria:** stream applications use honest stream semantics while sharing contracts, serialization, pipelines, and telemetry with the core runtime.

## Phase 7: Third Language Client

**Outcome:** the specification is proven beyond the CLR and JVM implementations.

Choose the language from concrete adoption needs:

- TypeScript for broad application and serverless usage
- Go for infrastructure and operational services
- Python for data, automation, and AI workloads

Start with the portable core, canonical fixtures, and one transport profile. Map every shared concept to a recognizable platform counterpart, but design packages, modules, concurrency, cancellation, lifecycle, and consumer APIs idiomatically. Generate data contracts or fixtures where useful; do not generate a client by mechanically translating another implementation.

**Exit criteria:** the client passes the same wire and interoperability suites and can be operated through the same introspection model.

## Phase 8: Integrations and Controlled Operations

**Outcome:** MyServiceBus connects to application delivery channels and, where justified, supports safe operational actions.

- Add SignalR as a durable-message-to-realtime bridge, not as a bus transport.
- Consider webhook and serverless hosting adapters using the same integration boundary.
- Design authentication, authorization, audit, idempotency, and confirmation rules for operational commands.
- Only then consider fault replay, purge, topology deployment, or remote configuration.

**Exit criteria:** integrations preserve their native semantics, and every mutating operational action has an explicit security and audit model.

## Candidate Backlog

The following work remains demand-driven and is not automatically part of the portable core:

- Amazon SQS/SNS transport profile
- SQL-backed transport
- ActiveMQ transport profile
- Azure Event Hubs stream adapter
- Azure Functions and AWS Lambda hosting adapters
- SignalR and webhook integrations
- additional monitoring persistence providers
- schema registry integrations and contract-evolution tooling
- controlled replay and topology-management APIs

## Near-Term Recommended Sequence

The next coherent investment is:

1. complete the mediator and in-memory stability gate in matching C# and Java slices
2. verify generated NuGet and Maven packages from isolated consumer projects
3. stabilize the inspection addon DTOs against the completed topology foundation without expanding the control plane
4. build focused monitoring state and event records
5. select a second durable broker only after the local-runtime gate, demonstrated demand, and capability-model validation

This sequence prioritizes predictable application fundamentals while reducing the architectural risk of adding transports, languages, or dashboard behavior too early.
