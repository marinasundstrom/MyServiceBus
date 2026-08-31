# MyServiceBus Roadmap

## Purpose

This roadmap turns the project direction into an incremental delivery plan. It is directional rather than a release commitment: phases are ordered by dependency and learning value, while dates should be assigned only when maintainers select work for a release.

MyServiceBus aims to become a modern, cross-language messaging runtime that:

- interoperates with MassTransit through explicit protocol and transport profiles
- provides a focused, production-grade messaging model across languages
- supports multiple broker families without erasing their differences
- exposes optional inspection and monitoring APIs for operational tools
- gives enterprise adopters explicit reliability, security, operability, and lifecycle guarantees

The roadmap is centered on replacing MassTransit in basic broker-backed messaging scenarios. It does not currently seek to turn MyServiceBus into a general-purpose publisher/consumer abstraction for technologies without service-bus topology.

It is positioned as a focused runtime for enterprises that need production-critical messaging without adopting the feature breadth of a large enterprise service-bus platform. Interoperability with MassTransit enables mixed systems and migration; it does not turn MassTransit's complete feature catalog into the destination of this roadmap. The [Enterprise Production Readiness](enterprise-readiness.md) gates define the evidence required before production-ready claims are made.

## Decision Guardrails

Use these rules when accepting roadmap work:

1. A portable-core feature must be specifiable independently of C# and implementable naturally in multiple languages.
2. MassTransit compatibility work must identify its target level: wire, semantic, transport profile, or API familiarity.
3. A transport must declare unsupported and emulated behavior; it must not silently reduce delivery guarantees.
4. C# and Java changes to shared behavior ship together or create an explicit, temporary parity entry.
5. Inspection and monitoring remain optional addons. Message delivery must not depend on a dashboard or central registry.
6. New language clients begin with conformance fixtures and one supported transport profile, not the full accumulated feature set.
7. A MassTransit feature is not added solely for feature parity. It must materially improve interoperability, migration, or the focused MyServiceBus user experience.
8. Prefer a coherent portable core with enterprise-grade guarantees over feature breadth; specialized patterns stay demand-driven.
9. Keep shared concepts and useful counterpart types recognizable across clients, but never derive Java packages or APIs mechanically from C# namespaces and language features, or vice versa.
10. Treat the normalized topology query model as a foundational API. Runtime provisioning, inspection, and dashboards must consume it rather than constructing separate topology interpretations.
11. Keep broker-backed service-bus semantics as the stable product boundary. Explore HTTP, webhooks, realtime sessions, and similar delivery mechanisms separately and generalize the core only from demonstrated shared requirements.
12. Treat mediator dispatch as an explicitly local execution mode. Externally observable events normally follow the broker-backed path.
13. Support one logical bus per application. Do not add multiple hosted buses solely for MassTransit compatibility; reconsider them only for a concrete cross-platform use case with an idiomatic Java dependency-injection model.
14. Prioritize delivery integrity, secure deployment, operability, resilience evidence, and a predictable support lifecycle ahead of dashboard depth, additional transports, or feature-catalog parity.
15. Treat production readiness as an evidence-backed status for a named release and capability set, not as a general description of the project.

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

The [Topology Model Specification](specs/topology-model-spec.md) defines the target boundary. This gate precedes expansion of inspection, dashboard, saga, outbox, and additional transport work. The later [Transactional Outbox and Inbox Specification](specs/outbox-inbox.md) now defines the transaction and provider-conformance boundary without changing topology identity.

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

**Status:** implemented for the current preview scope. All shared scenarios are verified in C# and Java, including lifecycle, scopes, requests, retries, filters, metadata, type dispatch, deterministic scheduling, independent consumer failure behavior, eventual consumed observations, directed send, publish fan-out, and the separate mediator type-routed single-handler `Send` contract with cardinality validation and result dispatch. Handler, consumer, and consumer-method registrations share that mediator boundary without changing the existing message-bus directed-send fan-out contract.

## Serialization Architecture Gate

**Outcome:** additional serializers can be added without changing central receive logic, while C# and Java retain corresponding MassTransit-familiar serializer stages and platform-native implementation details.

- Introduce clean serializer, deserializer, serializer-factory, message-body, and inbound-context boxes.
- Keep content-type matching and multi-deserializer selection in an immutable internal registry.
- Configure source-generated `System.Text.Json` metadata through the built-in .NET serializer without exposing it in the portable contract.
- Add the optional MassTransit BSON envelope profile in C# and Java with shared fixtures and live interoperability coverage.
- Verify explicit-metadata JSON paths under .NET Native AOT and GraalVM Native Image.

The target architecture, capability boundary, and delivery slices are defined in the [Serialization Architecture Proposal](proposals/serialization-architecture.md).

**Exit criteria:** existing JSON profiles retain their behavior; new formats register through corresponding factories in both clients; source-generated JSON works on send and receive with reflection disabled; and BSON passes the C#↔Java↔MassTransit matrix.

**Status:** in progress. Contracts, registry selection, configurable JSON metadata, the source-generated .NET NativeAOT smoke, optional C# and Java BSON artifacts, direct .NET↔MassTransit BSON decoding, and bidirectional C#↔Java BSON fixtures are implemented. The broader broker-backed BSON matrix and remaining AOT/native-image capability work are still open.

## Enterprise Production Gate

**Outcome:** supported runtime capabilities are safe to adopt for production-critical enterprise workloads and are backed by explicit operational evidence.

- Specify acknowledgement points, crash windows, duplicate behavior, and recovery outcomes for every supported transport operation.
- Add outbox/inbox and idempotency foundations, bounded concurrency, graceful draining, and failure-injection coverage in both clients.
- Complete secure broker configuration, managed identity, least-privilege guidance, vulnerability handling, SBOMs, and release provenance.
- Standardize OpenTelemetry metrics and transport health semantics, then publish incident, upgrade, rollback, and capacity runbooks.
- Establish broker-backed load, saturation, outage-recovery, and soak gates.
- Define the stable API, compatibility, deprecation, servicing, and support policy required for `1.0`.

**Exit criteria:** one immutable release candidate passes the applicable delivery, security, operations, resilience, mixed-version, package, and interoperability gates for both C# and Java.

The detailed assessment, work slices, and evidence rules are defined in [Enterprise Production Readiness](enterprise-readiness.md). Inspection and dashboard work may continue experimentally, but must not be promoted as a substitute for these runtime gates.

**Transactional consistency status:** Transactional Outbox MVP implemented for evaluation. Both clients provide scoped Bus Outbox capture, normalized PostgreSQL persistence, service-partitioned leasing, transport dispatch, delivery lifecycle composition, health/backlog inspection, optional dispatcher monitoring export, and deterministic real-database recovery evidence. The central collector and dashboard summarize embedded or standalone dispatcher backlog, lag, throughput, failures, and lease losses. The separate Aspire topology proves application-state-plus-outbox commits and bidirectional C#/Java consumption through RabbitMQ. Transparent Consumer Outbox middleware, cleanup, inbox monitoring, and complete process-level O01–O06 evidence remain open; no production-promotion claim is made yet.

**Scheduling status:** the default C# and Java message schedulers remain explicitly volatile and cancellable. Matching PostgreSQL providers persist one-time delayed intent transactionally, return its message identity as a handle, and support atomic persisted cancellation after commit. Matching recurring-job providers persist definitions and transactionally promote durable occurrences into tracked jobs. In-memory tracked promotion, occurrence monitoring, process-level restart evidence, broker-native adapters, and third-party provider conformance remain open before a general durable-scheduling promotion claim.

Future monitoring, recurring-job, alerting, and privileged-control ideas are collected without delivery ranking in the [Monitoring and Control Backlog](development/monitoring-and-control-backlog.md). A later planning pass will order them by user value and architectural dependency.

## Phase 3: Inspection and Monitoring APIs

**Outcome:** applications and tools can discover and observe a distributed MyServiceBus system without coupling clients to a UI or local monitoring store.

- Complete first-party inspection addons for C# and Java.
- Stabilize language-neutral DTOs for services, instances, endpoints, consumers, contracts, versions, and capabilities.
- Implement the monitoring addon described in the monitoring proposal.
- Expose general-purpose immutable hooks that any application or addon can implement.
- Keep collector-specific batching and export in the optional monitoring integration.
- Capture aggregate runtime state and high-signal records for retries, faults, skipped messages, and moves to error in a central service.
- Keep health endpoints and OpenTelemetry integrations distinct but discoverable.
- Define optional registration or heartbeat events for aggregating multiple runtime instances.

**Exit criteria:** equivalent C# and Java clients export compatible metadata and observations, and the central service exposes a programmatic distributed runtime model.

## Phase 4: Read-Only Dashboard

**Outcome:** operators can understand topology and runtime behavior across services.

- Build the dashboard solely against stable inspection, monitoring, health, and telemetry interfaces.
- Show services, instances, endpoints, consumers, contracts, and producer/consumer relationships.
- Show compatibility and capability warnings.
- Show retries, faults, skipped messages, error moves, and links to traces.
- Extend the central monitoring service with optional historical persistence.
- Define optional broker-metrics adapters for queue depth and broker-native health.

**Exit criteria:** the dashboard can visualize a mixed C#/Java system and remains useful when broker administration APIs are unavailable.

## Phase 5: Second Durable Broker Profile

**Outcome:** the transport model is proven against a managed broker with materially different semantics.

Azure Service Bus is the recommended first candidate because topics, subscriptions, sessions, native dead-lettering, and scheduling exercise the capability model. Final selection should follow demonstrated user demand and maintainer access.

The [Azure Service Bus transport profile](azure-service-bus-transport.md) fixes
the initial implementation and conformance boundary. Corresponding C# and Java
adapters now pass the pinned emulator suite and the live-Azure gate for cloud
topology, delivery, lock renewal, request/response, terminal failure settlement,
cross-language exchange, and bidirectional MassTransit interoperability.

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

1. run the complete preview release-candidate gate on one commit and publish only the scoped verified claims
2. specify delivery guarantees and add failure-injection coverage for process, network, settlement, and broker failures
3. integrate the portable outbox/inbox foundation with the pipelines and deliver the first transaction-backed C# and Java persistence providers
4. complete secure deployment, production telemetry and health, operational runbooks, and supply-chain evidence
5. establish broker-backed load, soak, saturation, and recovery gates
6. define the stable compatibility and support lifecycle, validate mixed-version upgrades, and prepare `1.0`
7. promote monitoring, dashboard, and additional transport work only after the production gates on which they depend

This sequence prioritizes the guarantees enterprise adopters need to approve and operate the runtime while reducing the architectural risk of adding transports, languages, or dashboard behavior too early.
