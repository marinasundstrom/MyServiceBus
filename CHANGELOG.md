# Changelog

This changelog summarizes the bigger themes in the repository history. It is intentionally thematic rather than exhaustive, and is based on work landed between April 4, 2025 and March 24, 2026.

## Unreleased

### Stable cross-language topology foundation

- Added corresponding versioned topology snapshots for C# and Java with stable identities, logical endpoint addresses, and canonical conformance fixtures.
- Added synchronized public snapshot-version constants and explicit additive/breaking evolution rules.
- Added corresponding RabbitMQ receive-topology projections that validate profile inputs before broker provisioning.
- Moved the C# and Java bus runtimes onto corresponding profile-neutral receive-endpoint transport topology contracts while retaining legacy transport overload adapters.
- Completed the topology stability gate with a prospective extension model for saga nodes, outbox policies, and materially different durable-broker projections.
- Made inspection consume the normalized topology snapshot and stopped inferring RabbitMQ-specific details that are not supplied by an authoritative transport projection.

### MVP API stabilization

- Declared profile-neutral receive-endpoint topology as the supported transport extension point and deprecated legacy C# and Java receive-transport overloads without removing compatibility.
- Made Java cancellation APIs idiomatic with method-based accessors, tokenless context construction, and a standard `CancellationException` guard while retaining the shared cancellation-policy concept.

### MVP dependency hygiene

- Updated Aspire hosting, ASP.NET Core OpenAPI, and OpenTelemetry package families to patched releases so the resolved MVP application dependency graph is clear of known NuGet advisories.
- Made .NET CI fail restoration when NuGet reports a low, moderate, high, or critical package advisory.

### MVP packaging

- Defined the four supported .NET artifacts as explicit `0.1.0-preview.1` NuGet packages with repository, license, description, readme, and symbol metadata; all non-package projects are excluded by default.
- Defined seven foundational Java modules as `0.1.0-preview.1` Maven publications with source, Javadoc, license, project, developer, and source-control metadata; preview inspection and sample applications remain unpublished.
- Scoped Java production dependencies to the modules that own them so published POMs do not expose unrelated broker, serialization, dependency-injection, logging, or telemetry libraries.
- Added NuGet and Maven package construction to the regular .NET and Java CI workflows.

### Product and hosting boundaries

- Defined broker-backed, basic MassTransit replacement scenarios as the stable product scope; positioned mediator as deliberately local execution and explicitly kept multiple hosted buses outside the supported application model.
- Made cross-language mediator and in-memory harness stability the first implementation priority before additional broker transports.
- Fixed Java mediator publication to resolve scoped endpoint services inside an active message scope and enabled its consumer and handler delivery tests to match the existing C# scenarios.
- Preserved Java consume-context routing in scoped send-endpoint providers across asynchronous consumer continuations.
- Made mediator handler snapshots and in-memory harness registration and consumption observations safe under concurrent dispatch in both reference clients.
- Defined the portable pipeline and filter execution contract and added matching C# and Java conformance scenarios for wrapping, short-circuiting, failures, and cancellation propagation.
- Kept Java consumer scopes alive through asynchronous pipeline completion and deterministically closed scoped services afterward.
- Added explicit operation-scoped filter registration with constructor injection and asynchronous disposal in both reference clients.
- Added matching immutable pipeline and filter descriptors for validation and future inspection without exposing runtime middleware objects.
- Corrected Java publish filters to use `PublishContext` and verified matching publish-then-send-then-transport ordering in both clients.
- Verified matching mediator consume-filter wrapping and downstream-only retry re-entry in C# and Java.
- Verified matching mediator retry-exhaustion attempt counts, filter observations, and terminal failure propagation.
- Made Java fixed-delay retries react immediately to cancellation, matching C# behavior and preventing another attempt.
- Verified that mediator consumer failures are attempted once and propagate immediately when retry is not configured.
- Added the shared C# and Java mediator/in-memory conformance matrix, identifying verified behavior and the remaining stability gaps.
- Defined matching explicit, idempotent lifecycle behavior for the C# and Java in-memory test harnesses while keeping standalone mediators immediately usable.
- Defined the same stopped, started, restart, and failed-start recovery semantics for hosted C# and Java buses, including explicit rejection of outbound work while stopped.
- Verified distinct dependency-injection scopes per consumer delivery in both in-memory harnesses and kept scoped resources alive through asynchronous completion and disposal.

## 2026-03-24 to 2026-03-19

### Aspire, runtime modernization, and parity cleanup

- Added Aspire-based local orchestration work, including RabbitMQ configuration and shared service defaults.
- Upgraded the .NET stack to .NET 10.
- Reworked telemetry setup and brought logging behavior closer to parity between C# and Java.
- Refactored message conventions and serialization, including fixes for raw message serialization.
- Updated sample scenarios to keep the C# and Java experiences aligned.
- Removed use of the CheckedExceptions analyzer as the Java implementation matured.

## 2025-12-16 to 2025-10-30

### Developer experience and hosting direction

- Added `AspireApp`, establishing the direction for orchestrated local development.
- Expanded top-level documentation, including README updates, Java FAQ material, and feature walkthrough improvements.
- Continued tightening the onboarding story for both languages without introducing a large new feature wave.

## 2025-09-10 to 2025-09-09

### Scheduling, bus factory cleanup, and logging ergonomics

- Added message scheduling support and follow-up test tolerance fixes in Java.
- Refined the bus factory surface, including a more self-contained factory model and Java API alignment.
- Renamed `IScopedClientFactory` to `IRequestClientFactory` to better reflect intent.
- Added consumer factory configuration and improved service collection integration patterns.
- Introduced default console logging across the bus and added Java logging builder support.
- Moved the Gradle build to the repository root and improved related documentation.

## 2025-09-08 to 2025-09-07

### Reliability, topology, formatting, and observability

- Strengthened RabbitMQ failure handling with fault queues, skipped queues, requeue behavior, delivery acknowledgement fixes, and health checks.
- Replaced naming conventions with formatter-based customization, including message entity name formatters.
- Added per-endpoint serializer configuration and anonymous message support in Java.
- Allowed multiple consumers per queue or per message type and tightened duplicate registration handling.
- Added OpenTelemetry instrumentation and expanded the logging abstractions and documentation.
- Added and refined architecture, pipeline, exception-handling, and portability documentation to support the larger design direction.

## 2025-09-06 to 2025-09-05

### Build, testing, documentation, and cross-language governance

- Migrated the Java build to Gradle and then improved Gradle usage, properties, and repository layout.
- Added CI workflows for .NET and Java, plus markdown-only workflow skipping and manual workflow dispatch support.
- Formalized project guidance around cross-language parity, testing expectations, API visibility, and error-handling strategy.
- Expanded the documentation set substantially with development guides, architecture notes, migration guidance, parity checklists, and testing guidance.
- Added OpenTelemetry instrumentation and made retries opt-in across C# and Java.
- Improved serializer, header, error transport, and envelope behavior to better match MassTransit semantics.

## 2025-09-04 to 2025-09-02

### Request/response, DI, transport abstraction, and test harnesses

- Added scoped request client factories in both C# and Java and expanded request/response handling, including fault responses and temporary reply endpoints.
- Added RabbitMQ bus configurators and refactored transport-specific configuration around a more transport-agnostic core.
- Built out dependency injection support, including scoped services, send endpoint providers, Guice integration, and host/container-based registration.
- Added in-memory mediator transports and test harnesses for both .NET and Java, with broader consumer and fault-handling coverage.
- Introduced pipeline filters, retry support, configurable serializers, and topology registry work in Java.
- Expanded quick-start and feature walkthrough material to keep the new abstractions usable.

## 2025-09-01

### First major parity push

- Established the initial cross-language structure for C# and Java under a shared MyServiceBus model.
- Added in-memory mediator transports, endpoint configuration, fault handling, retry filters, batch support, and consumer startup behavior.
- Documented shared messaging concepts, Java capabilities, exception-handling expectations, and repository contributor instructions.
- Added AGENTS guidance for the repository and for the Java subtree to reinforce parity and documentation discipline.

## 2025-07-24 to 2025-07-18

### Analyzer and dependency maintenance

- Updated the CheckedExceptions-related packages and settings during the period when checked-exception guidance was still part of the Java workflow.
- Added unit tests for dependency injection and reorganized supporting code.

## 2025-04-13 to 2025-04-04

### Project foundation and first usable clients

- Created the repository, initial .NET solution, and Java projects.
- Established the core messaging API shape, topology concepts, and host configuration direction.
- Built the first working C# and Java client prototypes.
- Added early Java dependency injection work, consumer handling, and scoped dependency fixes.
- Introduced request/response support in C#, including `GenericRequestClient`, cancellation, timeout handling, and request client tests.
- Added initial README and setup documentation to make the project runnable.

## Maintenance policy

Keep this file updated for significant changes. Prefer adding dated entries that summarize the main themes of a change set instead of listing every commit.
# Unreleased

- Added sample-app dashboard endpoints in the .NET and Java `TestApp` projects under `/dashboard/v1/*`, exposing stable JSON snapshots for bus overview, messages, consumers, and topology without committing those contracts to the shared libraries yet.
- Split the programmatic inspection surface into first-party addon projects for .NET and Java, keeping the sample inspection endpoints working while removing the core bus packages' direct dependency on inspection registration.
- Documented the long-term architecture and phased roadmap, including explicit compatibility levels, capability-aware transport profiles, event-stream and SignalR integration boundaries, cross-language conformance, and the optional inspection, monitoring, and dashboard plane.
- Added shared versioned message, request, and fault fixtures with C# and Java validation, plus Testcontainers-backed RabbitMQ transport round-trip tests as the first executable compatibility baseline.
- Added bidirectional C#↔Java RabbitMQ interoperability tests, a dedicated CI job, and configurable AMQP ports so normal client configuration works with dynamically mapped Testcontainers endpoints.
- Extended the RabbitMQ conformance matrix with verified C#↔MassTransit and Java↔MassTransit envelope delivery in both directions.
- Added correlated C#↔MassTransit request/response conformance and aligned C# and Java request envelopes on explicit request identifiers.
- Completed the Java↔MassTransit request/response matrix, aligned temporary RabbitMQ endpoint addressing, and documented MyServiceBus as a focused interoperable alternative rather than an enterprise feature-parity competitor.
- Completed bidirectional C# and Java fault-response conformance with MassTransit, including canonical generic fault URNs, correlated routing, strict response-type discrimination, and MassTransit fault field names.
- Added live RabbitMQ conformance coverage for C# and Java retry exhaustion and MassTransit-readable `_error` and `_skipped` queue delivery.
- Introduced matching versioned transport capability descriptors for C# and Java, with RabbitMQ and in-memory profiles using `native`, `emulated`, and `unsupported` classifications.
- Added opt-in startup capability requirements in both clients, including the ability to require native support and clear failures before receive transports start.
- Moved publish and temporary response address production behind transport factories, removing hard-coded RabbitMQ addresses from the C# request client and configured-host assumptions from Java RabbitMQ envelopes.
- Routed bus-level and consume-context publication through transport-provided address producers in both clients, keeping broker URI structure out of portable publish behavior.
- Defined cross-language conceptual parity as recognizable counterpart abstractions with idiomatic platform APIs and code organization, explicitly rejecting mechanical namespace/package and source translation.
- Moved error and fault address production behind corresponding C# and Java transport-factory methods, eliminating portable-core and RabbitMQ receive-path assumptions about broker hostnames.
- Replaced Java convenience-context RabbitMQ path inference with transport-neutral logical addresses and added matching logical `exchange:`/`queue:` resolution to the Java RabbitMQ adapter.
- Marked the transport-capability foundation complete and revised the new-transport guide so terminal delivery, addressing, capabilities, and conformance are profile-driven rather than RabbitMQ-shaped.
- Added Testcontainers-backed C# and Java directed-send conformance to MassTransit RabbitMQ receive endpoints, separating queue-address evidence from publish interoperability.
- Completed the RabbitMQ directed-send matrix with MassTransit-to-C#, MassTransit-to-Java, and bidirectional C#↔Java queue-address delivery.
- Defined a normalized, queryable cross-language topology model as a stability gate before inspection, dashboards, sagas, outbox support, and additional transports.
- Added matching versioned topology snapshot APIs in C# and Java with deterministic message, endpoint, consumer, and binding identities, logical addresses, and immutable Java views.
- Added corresponding receive-endpoint definitions to both topology registries so snapshots query normalized endpoint intent instead of embedding durability defaults in snapshot builders.
