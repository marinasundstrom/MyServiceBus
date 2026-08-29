# Enterprise Production Readiness

## Product Direction

MyServiceBus is intended for enterprises that run business-critical, broker-backed messaging across .NET and Java services. The goal is not to reproduce every feature of a large enterprise service bus. The goal is to provide a focused runtime whose supported capabilities have explicit guarantees, operational evidence, secure deployment guidance, and a predictable lifecycle.

Enterprise readiness is a release standard, not a feature count. A capability is production-ready only when its behavior is specified, implemented in both reference clients where portable behavior is involved, exercised under failure, observable in operation, and covered by upgrade and support guidance.

## Current Assessment

The repository has a strong interoperability and conformance foundation, but the published packages remain previews. The assessment below distinguishes evidence already present from work required before recommending MyServiceBus broadly for production-critical workloads.

| Area | Current evidence | Readiness | Required next step |
| --- | --- | --- | --- |
| Cross-language contracts | Versioned protocol and topology fixtures; C# and Java parity rules | Strong foundation | Add mixed-version rolling-upgrade tests for every supported release pair |
| Broker interoperability | Bidirectional C#/Java/MassTransit matrices for RabbitMQ and Azure Service Bus within pinned versions | Strong foundation | Define the stable support matrix and repeat it for each release candidate |
| Delivery failures | Retries, faults, `_error` and `_skipped` destinations, lock renewal, and explicit Azure at-least-once boundary | Partial | Document every acknowledgement and crash window; add failure-injection coverage |
| Transactional consistency | Prospective topology model only | Gap | Provide outbox/inbox contracts and supported persistence integrations |
| Idempotency | Duplicate risk is documented for Azure compatibility moves | Gap | Provide a portable message identity and deduplication strategy with tested storage semantics |
| Lifecycle and flow control | Explicit timed shutdown, portable endpoint concurrency limits, RabbitMQ drain-on-shutdown, prefetch configuration, and Azure lock renewal | Partial | Add saturation and forced-stop broker evidence, bounded callback queues, and broker-outage recovery gates |
| Observability | W3C trace propagation, OpenTelemetry spans, hooks, experimental monitoring, and RabbitMQ health checks | Partial | Define stable metrics, health semantics for both transports and languages, alert guidance, and cardinality limits |
| Transport security | Username/password and connection-string configuration are available | Gap | Add TLS validation guidance and tests, secret rotation patterns, least-privilege broker policies, and managed identity for Azure |
| Monitoring security | Payloads are excluded, but the collector is explicitly unauthenticated and in-memory | Experimental | Add authentication, authorization, TLS, request limits, durable retention, and audit behavior before production use |
| Supply chain | NuGet advisory failures, staged package consumers, trusted NuGet publishing, and signed Maven artifacts | Partial | Publish SBOMs and provenance, scan source/images/dependencies, document vulnerability reporting and response |
| Performance | Reproducible local dispatch and AOT benchmarks | Partial | Add broker-backed throughput/latency, resource, saturation, long-run, and recovery benchmarks |
| Release lifecycle | Pinned preview baseline and coordinated release instructions | Preview only | Define `1.0` compatibility, deprecation, servicing, security-fix, and end-of-support policies |
| Operations | Topology modes and detailed transport profiles | Partial | Publish deployment, upgrade, rollback, incident, poison-message, and capacity runbooks |

This assessment does not make the existing preview unsuitable for evaluation or controlled workloads. It means production adoption should be an explicit risk decision until the applicable gates below are complete.

## Enterprise Release Gates

### Gate 1: Delivery Integrity

**Outcome:** an application team can explain what happens to every message across success, handler failure, process termination, network ambiguity, and broker outage.

- Define the delivery guarantee and acknowledgement point for send, publish, consume, retry, error moves, skipped moves, requests, and responses on each transport.
- Identify duplicate and loss windows explicitly; never imply exactly-once delivery.
- Add first-class transactional outbox and inbox/idempotent-consumer contracts with corresponding C# and Java behavior.
- Select narrowly supported persistence integrations based on production demand and verify schema migration, cleanup, contention, and recovery behavior.
- Validate bounded endpoint concurrency independently of broker prefetch under sustained saturation.
- Drain in-flight work during graceful shutdown with a configurable deadline and an explicit forced-stop outcome.
- Exercise process termination, connection loss, broker restart, lock expiry, ambiguous settlement, and partial error-move failures in automated tests.

**Exit criteria:** both clients pass the same failure matrix, and the transport profiles state the resulting application-visible guarantee for each operation.

### Gate 2: Secure Deployment and Supply Chain

**Outcome:** platform and security teams can deploy the runtime without inventing an unsupported security model.

- Support and test TLS broker connections, certificate validation, and failure behavior in both clients.
- Document secret injection and rotation without logging credentials or placing them in topology and monitoring metadata.
- Add token-credential and managed-identity configuration for Azure Service Bus; retain connection strings as an explicit compatibility option.
- Publish least-privilege permissions for `Create` and `PreProvisioned` topology modes.
- Add a security policy covering private vulnerability reports, supported versions, response expectations, and disclosure.
- Generate release SBOMs and build provenance for packages and container images; add dependency, source, secret, and image scanning to release evidence.

**Exit criteria:** the secure reference deployments pass integration tests, release artifacts are traceable to one source commit, and consumers have a documented vulnerability channel.

### Gate 3: Production Operations

**Outcome:** operators can detect degradation, distinguish application failures from broker failures, and perform routine recovery safely.

- Define stable OpenTelemetry span names, attributes, metrics, units, and cardinality constraints.
- Expose health semantics for every supported transport in both clients, separating liveness, readiness, and degraded states.
- Cover connection state, endpoint state, message rates, consume duration, retry/fault counts, in-flight work, saturation, and dropped telemetry.
- Publish alerting signals and initial thresholds as guidance rather than universal defaults.
- Add runbooks for broker outage, poison messages, backlog growth, credential expiry, topology mismatch, deployment rollback, and mixed-version upgrades.
- Keep message delivery independent of the MyServiceBus monitoring service. Do not label that service production-ready until authentication, authorization, request limits, durable storage, retention, and high-availability behavior are implemented.

**Exit criteria:** an operator can diagnose and rehearse the supported incident scenarios using documented telemetry and procedures without attaching a debugger.

### Gate 4: Scale and Resilience Evidence

**Outcome:** capacity decisions and failure expectations are backed by reproducible measurements.

- Establish broker-backed benchmarks for throughput, end-to-end latency percentiles, allocations or heap pressure, CPU, and memory.
- Measure normal load, saturation, backlog recovery, slow consumers, broker interruption, and telemetry failure.
- Run multi-hour soak tests with duplicate, loss, ordering-scope, resource-growth, and shutdown assertions.
- Publish the complete configuration, hardware or runner characteristics, broker version, payload profile, and limitations with every result.
- Define release-blocking regression budgets after the baseline is stable.

**Exit criteria:** each supported transport has reproducible C# and Java load evidence and no unexplained message loss or unbounded resource growth in the declared test envelope.

### Gate 5: Stable Lifecycle and Adoption

**Outcome:** an enterprise can approve the dependency with a predictable upgrade and support model.

- Freeze the intended `1.0` portable API, protocol, topology, capability-descriptor, and transport-profile contracts.
- Define semantic versioning, deprecation duration, breaking-change rules, patch support, security servicing, and end-of-support notice.
- Test rolling upgrades and downgrades across the supported C#/Java and protocol-version combinations.
- Publish architecture, production configuration, deployment, migration, troubleshooting, and compatibility documentation as one coherent adoption path.
- Provide an ownership and support model with a triage process for production-impacting defects.

**Exit criteria:** one release candidate passes every applicable enterprise gate on the same commit, and the support policy is published before `1.0` is announced.

## Prioritized Delivery Sequence

Work should proceed in this order:

1. Specify delivery guarantees and build the failure-injection matrix.
2. Complete broker-backed forced-stop and saturation evidence for bounded concurrency and graceful draining in C# and Java.
3. Design and implement the portable outbox/inbox boundary, then add the first supported persistence integration for each ecosystem.
4. Complete secure transport configuration, managed identity, least-privilege guidance, and supply-chain evidence.
5. Standardize metrics and cross-transport health semantics; publish the core operational runbooks.
6. Establish broker load, soak, saturation, and recovery gates.
7. Declare the stable compatibility and support policy, run mixed-version release validation, and prepare `1.0`.
8. Promote the optional monitoring service only through a separate production gate; continue dashboard features and additional transports after the runtime gates they depend on.

Sagas, orchestration, additional brokers, stream transports, and control-plane operations remain demand-driven. They must not displace delivery integrity, security, operability, and lifecycle work required by production adopters.

## Evidence Rules

- Use **verified** only for a named version, transport, language, and scenario covered by an automated gate.
- Keep unsupported and emulated behavior visible at startup and in documentation.
- Treat expected duplicate delivery as a normal distributed-systems condition and give applications a supported mitigation path.
- Do not infer production readiness from unit tests, local mediator benchmarks, or broker happy-path interoperability alone.
- Preserve C# and Java parity for portable behavior; record any temporary exception with an owner and removal target.
- Run the enterprise release gates on the same immutable candidate commit used to build every package and image.

The [roadmap](roadmap.md) orders this work alongside the existing protocol, transport, inspection, and monitoring phases. The [compatibility policy](compatibility.md) defines the scope of interoperability claims, and [supported versions](supported-versions.md) records the current preview baseline.

The first delivery-integrity artifacts are the [Delivery Guarantees Specification](specs/delivery-guarantees.md) and its executable [Delivery Failure Matrix](development/delivery-failure-matrix.md).
