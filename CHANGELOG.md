# Changelog

This changelog summarizes the bigger themes in the repository history rather than every individual commit.

## Unreleased

- Aligned C# and Java outbound serializer contracts with MassTransit's message-body model, added corresponding byte-array body implementations, and moved transport byte materialization behind that boundary.
- Proposed a bidirectional serialization registry that separates envelope protocols from application payload metadata, defines source-generated JSON and strict AOT boundaries, and scopes BSON to an optional cross-language MassTransit compatibility profile.

## 0.1.0-preview.5 - 2026-08-28

- Reworked the website concepts area into concise technical pages for contracts, intent, receive endpoints, topology, dispatch, requests, and reliability, with prose stored separately as MDX and fenced samples rendered through Monaco.
- Rebuilt the landing-page hero around a responsive messaging diagram and promoted its cream, deep-green, mint, and coral palette into shared site-wide design tokens with corresponding dark-theme contrast.
- Defined consumer-method endpoint precedence consistently across C# and Java: fluent overrides method attributes, method attributes override class attributes, and explicit endpoints override conventions and formatters.
- Grouped all consumer-method bindings for one endpoint into a single receive transport, preserving multi-method dispatch in generated, reflection, and mediator paths.
- Added C# static-container fluent registration and retained method-name conventions for bare method attributes, including generated catalogs.
- Added automatic request-response semantics for C# `Task<T>`/`ValueTask<T>` and Java `CompletableFuture<T>`/`CompletionStage<T>` consumer methods in both reflection and generated dispatch.
- Added a preview-pinned .NET 11 NativeAOT smoke that suspends and resumes a Runtime Async consumer method through generated dispatch.
- Added an opt-in .NET 11 Runtime Async target for the core abstractions and mediator runtime while preserving .NET 10 as the default build and package target.
- Completed the Java bus-factory container integration path with explicit endpoint consumer mappings for RabbitMQ and Azure Service Bus, allowing `ConsumerFactory` to resolve application consumers without binding them into the bus's isolated provider, and documented async-safe scope ownership.

### NativeAOT registration

- Added the first .NET NativeAOT registration path: an incremental source generator now emits typed consumer catalogs, and consumer registration, broker endpoint configuration, retry configuration, and inbound consume-context construction avoid runtime generic reflection when using generated or explicit typed registration.
- Kept reflection-based consumer and assembly discovery as a supported convenience mode while annotating its dynamic-code and trimming requirements; generated catalogs provide the optimized NativeAOT alternative.
- Added a matching explicit Java consumer/message registration overload so manual and future generated catalogs share the same capability boundary without requiring Java to adopt Roslyn-style tooling.
- Added a framework-neutral Java JSR 269 processor that emits ordinary consumer catalogs and direct method invokers, plus a GraalVM Native Image mediator smoke test in Java CI.
- Added factory-based generated adapter activation on .NET so generated consumer methods execute in a BenchmarkDotNet NativeAOT child process without reflective constructor discovery.
- Added a dedicated .NET NativeAOT CI application that publishes and runs generated mediator dispatch, giving both clients an end-to-end native executable smoke test.
- Added service-provider serializer factories in .NET and Java, plus a Java deserializer factory, so AOT applications can construct custom serialization extensions without reflection.
- Added `ServiceCollection.createAot()` as a factory-only Java container and removed tracing-agent metadata from the GraalVM mediator smoke path; the conventional Guice-backed container remains available. No-argument service factories can bridge an existing Java container without exposing the MyServiceBus provider abstraction.
- Added reproducible BenchmarkDotNet and JMH harnesses. Local proof-of-concept results show lower typed-registration cost in both clients, while NativeAOT and GraalVM native steady-state dispatch still trail their JIT counterparts and remain optimization work.
- Clarified that reflection is an AOT reachability and trimming responsibility rather than an automatic blocker; generated catalogs reduce the preservation metadata and retained code required from the application.
- Consolidated the website's AOT proof, benchmark results, methodology, and remaining-work boundary on one page, while moving general consumer-method and reflection-discovery guidance out of the AOT narrative.
- Standardized every standalone website code sample on the read-only Monaco viewer with language-aware syntax support and accessible labels.

### Java adoption

- Clarified the two Java adoption paths: prefer the established MyServiceBus-first decorator style for new projects and the bus-factory boundary when integrating with an existing application's container.
- Added concrete Spring, Jakarta CDI, Dagger, and application-owned factory examples that keep the existing container responsible for consumer construction, injection, and lifetime management without connecting it to the included Guice implementation.
- Replaced the custom zero-argument Java service-factory contract with the JDK-standard `Supplier`, allowing `javax.inject.Provider`, `jakarta.inject.Provider`, Spring `ObjectProvider`, Dagger providers, and application factories to adapt through method references without selecting one DI namespace.
- Clarified that Java's service collection and provider contracts are the stable programming model: applications may use the included implementation, materialize the same registrations through another framework's adapter, or choose the direct bus-factory boundary. Documented the adapter mapping for singleton, per-message scoped, transient, cleanup, provider-aware factory, and multi-binding semantics.

### Attributed consumer methods

- Added `[Consumer]` declarations for static methods, instance methods, and classes containing eligible methods without requiring `IConsumer<T>`.
- Added message, consume-context, cancellation-token, and scoped service parameter binding with equivalent reflection discovery and generated typed adapters.
- Added class-level attributes and explicit `AddConsumerMethods<TConsumer>(...)` registration as alternatives for discovering and mapping method consumers without an `IConsumer` marker.
- Made grouped static methods on an attributed class the primary standalone-consumer shape, allowing several message methods to share one endpoint.
- Allowed `[Consumer("endpoint")]` on `IConsumer<T>` classes to override their endpoint mapping in reflection and generated catalogs without duplicate method registration.
- Documented declaration guidance based on consumer size and grouping, noting namespace-level functions only as a consideration for an external Raven integration.
- Confirmed method-level `[Consumer]` as a complete declaration without a class attribute and added optional type-filtered assembly discovery.
- Added a public platform-parity page that separates .NET runtime, C# generator, and Java runtime/tooling support.
- Added corresponding Java `@MessageConsumer` declarations, explicit reflection registration, endpoint overrides, parameter binding, service injection, direct generated invocation, and mediator dispatch without introducing classpath scanning or a mandatory framework.

### NServiceBus interoperability

- Separated neutral Raw JSON serialization from an explicit NServiceBus JSON compatibility profile in both C# and Java.
- Added NServiceBus message headers, intents, contract-identity overrides, case-insensitive inbound JSON, and RabbitMQ conventional endpoint routing in both clients.
- Added live RabbitMQ directed-send tests in both directions between NServiceBus and the C# and Java MyServiceBus clients, pinned to NServiceBus `10.2.8` and its RabbitMQ transport `11.2.1`.
- Added an isolated Aspire test stack with a real NServiceBus peer and a MyServiceBus peer configured for the NServiceBus profile, and documented the verified boundary without implying untested features.

### Runtime monitoring direction

- Published the optional inspection and monitoring client integrations as corresponding NuGet and Maven packages, with the collector and dashboard distributed independently as versioned multi-architecture container images.
- Added an end-to-end monitoring proof of concept with a central in-memory service, HTTP ingestion and query APIs, WebSocket invalidations, and a standalone Blazor dashboard orchestrated through Aspire.
- Added corresponding general-purpose immutable bus hooks for C# and Java; hook failures are isolated and the extension point is independent of the monitoring collector.
- Added optional C# and Java monitoring exporters that self-register metadata, emit heartbeats, and batch observations through bounded local queues without placing remote I/O in the messaging pipeline.
- Removed the sample applications' experimental embedded inspection endpoints and local dashboard state; both samples now export to the shared monitoring service.
- Added collector aggregation, lease tracking, batch deduplication, recent observations, and C#/Java hook and exporter coverage.
- Added automatic replica grouping, bounded resource labels, retry observations, per-application and per-instance windowed rates, consume latency, completeness indicators, observed message flow, and bounded real-time series queries.
- Expanded the Blazor prototype with label-grouped application views, replica load comparisons, live throughput graphs, and recent retry and failure detail driven by WebSocket invalidations with a polling fallback.
- Added expandable failed-message inspection for endpoint, retry, exception, correlation, conversation, and trace metadata while keeping payload and arbitrary-header capture out of scope.
- Validated the complete Aspire stack with live C# and Java traffic, fault observations, trace correlation, HTTP queries, WebSocket invalidations, and the Blazor dashboard; fixed endpoint discovery, exporter lifetime/draining, topology remapping, and Java timestamp interoperability defects found by that exercise.
- Added runtime-monitoring guides to the repository documentation and public documentation website, including the experimental MVP and production-readiness boundaries.
- Proposed a collector-style monitoring pipeline in which optional C# and Java hook handlers enqueue bus metadata, heartbeats, and bounded observation batches for export to a central monitoring service.
- Assigned instance registration, aggregation, flow analysis, query APIs, and all future persistence to the monitoring service while keeping client applications free of monitoring history.
- Defined the standalone Blazor dashboard as a consumer of the monitoring query API and aligned the vocabulary with MassTransit's monitoring, observability, observer, metrics, flow, and dashboard terminology.
- Kept OpenTelemetry collection separate and reserved optional dashboard providers for trace links and external telemetry queries.
- Scoped the implementation to MyServiceBus-specific hooks, export, aggregation, and query models while reusing existing infrastructure for telemetry, security, live updates, and persistence.
- Defined bounded exporter batching by interval, observation count, and payload size, with independent heartbeats, retry backoff, drop reporting, and a short shutdown flush.

### Azure Service Bus transport groundwork

- Defined the proposed Azure Service Bus transport profile, including initial capabilities, addressing, topology projection, message-property mapping, peek-lock settlement, compatibility error destinations, request/response, and the cross-language conformance boundary.
- Added a pinned Docker Compose emulator fixture with declarative queues, topics, forwarding subscriptions, health readiness, and structural validation for future C# and Java transport tests.
- Added corresponding experimental C# and Java Azure Service Bus adapters with MassTransit-familiar configuration, queue and topic addressing, profile-neutral topology projection, native message mapping, explicit peek-lock settlement, and pre-provisioned emulator mode.
- Added matching emulator-backed direct-delivery, publish/forwarding, and public factory-configuration tests, while reporting unimplemented request/response and temporary-endpoint behavior as unsupported.
- Added both adapters to the NuGet and Maven package, verification, release-bundle, and consumer-smoke surfaces.
- Aligned direct Java bus publication with C# by resolving the singleton transport endpoint directly while retaining send logging; scoped endpoint providers remain reserved for operations that carry a consumer or application scope.
- Added emulator-backed C#↔Java directed-send and publish conformance, plus matching retry recovery/exhaustion, `_error` and `_skipped` settlement, and endpoint-fault checks for both clients.
- Added corresponding Azure Service Bus request composition in C# and Java, including transport-produced temporary endpoint addresses, correlated responses and faults, pre-provisioned emulator mapping, and bidirectional C#↔Java request conformance.
- Added bidirectional Azure Service Bus conformance for MassTransit envelope metadata, application headers, and corresponding native broker properties.
- Added corresponding C# and Java Azure Service Bus competing-consumer conformance with duplicate-delivery detection.
- Added an opt-in cloud smoke gate for C# and Java consumption of messages sent by the pinned MassTransit Azure Service Bus transport.
- Added secret-free Azure CLI lifecycle orchestration, including failure-safe ephemeral namespace teardown, and live Standard-tier acceptance tests that prove C# and Java Create-mode topology provisioning, publication, forwarding, consumption, and cleanup.
- Extended the live gate with corresponding C# and Java correlated request/response scenarios that inspect native temporary response queues and verify their auto-delete configuration.
- Made the pinned MassTransit live-Azure gate self-provisioning and collision-free, and verified its messages are consumed by both the C# and Java MyServiceBus clients.
- Made per-message entity-name configuration authoritative for publication and transport-composed requests in both implementations, including corresponding RabbitMQ resolution, after live Azure exposed the previous default-name fallback.
- Added live Azure delivery-lock renewal coverage for C# and Java using handlers that outlive the entities' initial locks, with single-delivery and settlement verification.
- Defined bidirectional MassTransit interoperability and matching transport naming as promotion requirements for every supported broker profile, and published the experimental Azure Service Bus status and evidence on the documentation website.
- Aligned the default Azure Service Bus message-entity formatter with MassTransit in both clients, including namespace/package, nested-type, generic-type, and array conventions where the host type system exposes them, while keeping formatter configuration scoped to the transport.
- Added live bidirectional default-name Azure publication between MassTransit and both MyServiceBus clients, using corresponding C# and Java contract identities and public client APIs.
- Completed the live Azure directed-send matrix with public C# and Java MyServiceBus sends to MassTransit queues, complementing the verified reverse directions.
- Verified live correlated MassTransit responses to C# and Java MyServiceBus request clients through unique native Azure Service Bus response queues.
- Completed the live Azure request/response matrix with correlated C# and Java MyServiceBus responses to MassTransit request clients.
- Completed the live bidirectional Azure fault matrix between MassTransit and the C# and Java MyServiceBus request clients and services.
- Aligned automatic consumer endpoint naming with MassTransit across C# and Java and both broker transports, including consumer-type derivation and suffix trimming, and verified the resulting Azure queues, subscriptions, and companion entities live.
- Completed the Azure Service Bus promotion gate by verifying live C# and Java terminal failures preserve the original MassTransit request in `_error`, publish a correlated fault, and complete the source delivery; the documented initial profile is now a verified preview with explicit limitations.

### Documentation website

- Expanded the Core concepts guide around transport-neutral contracts, intent, endpoints, settlement, envelopes, and failure behavior, with publish-versus-send examples and inline RabbitMQ and Azure Service Bus mappings.
- Added practical guides for distributed-systems fundamentals, choosing asynchronous messaging, modeling communication before selecting a broker, and evaluating MyServiceBus as a community-driven preview alongside MassTransit and direct broker APIs.
- Added a focused public documentation website with a C#/Java switch for the introduction and getting-started examples.
- Curated the published information architecture around core messaging concepts, RabbitMQ, testing, and verified interoperability while keeping development documentation repository-only.
- Added an independently triggered GitHub Pages deployment workflow that does not restore, build, test, or release the .NET and Java projects.
- Added a browser-only light/dark theme switch that respects the visitor's system preference and remembers an explicit choice locally.
- Reworked the public Azure Service Bus page into an application-facing setup guide for provisioning, secret handling, C# and Java configuration, topology ownership, interoperability, and teardown while retaining maintainer conformance detail in the repository development docs.
- Added browser-side Google Analytics 4 measurement for the public documentation site.
- Made website deployment explicitly dispatched so release documentation can be published after its matching NuGet and Maven packages.

### Stable cross-language topology foundation

- Added corresponding versioned topology snapshots for C# and Java with stable identities, logical endpoint addresses, and canonical conformance fixtures.
- Added synchronized public snapshot-version constants and explicit additive/breaking evolution rules.
- Added corresponding RabbitMQ receive-topology projections that validate profile inputs before broker provisioning.
- Moved the C# and Java bus runtimes onto corresponding profile-neutral receive-endpoint transport topology contracts while retaining legacy transport overload adapters.
- Completed the topology stability gate with a prospective extension model for saga nodes, outbox policies, and materially different durable-broker projections.
- Made inspection consume the normalized topology snapshot and stopped inferring RabbitMQ-specific details that are not supplied by an authoritative transport projection.

### MVP API stabilization

- Added MassTransit-compatible conversation and initiator identifiers across C#, Java, local runtimes, and serialized envelopes.
- Aligned consumer-initiated publish cancellation inheritance across C# and Java while keeping arbitrary headers and outbound correlation explicit, matching MassTransit semantics.
- Exposed request and correlation identifiers to consumers, preserved request identifiers through responses, and isolated Java response matching by request identifier.
- Aligned C# and Java request timeout and caller-cancellation behavior, including deadline-free requests.
- Declared profile-neutral receive-endpoint topology as the supported transport extension point and deprecated legacy C# and Java receive-transport overloads without removing compatibility.
- Made Java cancellation APIs idiomatic with method-based accessors, tokenless context construction, and a standard `CancellationException` guard while retaining the shared cancellation-policy concept.

### MVP dependency hygiene

- Updated Aspire hosting, ASP.NET Core OpenAPI, and OpenTelemetry package families to patched releases so the resolved MVP application dependency graph is clear of known NuGet advisories.
- Updated the RabbitMQ Testcontainers dependency to resolve the patched SSH.NET release required by the release advisory gate.
- Made .NET CI fail restoration when NuGet reports a low, moderate, high, or critical package advisory.

### MVP packaging

- Defined the four supported `Sundstrom.MyServiceBus` .NET artifacts as explicit `0.1.0-preview.1` NuGet packages with repository, license, description, readme, and symbol metadata; all non-package projects are excluded by default.
- Defined seven foundational Java modules as `0.1.0-preview.1` Maven publications with source, Javadoc, license, project, developer, and source-control metadata; preview inspection and sample applications remain unpublished.
- Scoped Java production dependencies to the modules that own them so published POMs do not expose unrelated broker, serialization, dependency-injection, logging, or telemetry libraries.
- Added NuGet and Maven package construction to the regular .NET and Java CI workflows.
- Added a NuGet preview publication workflow using NuGet.org trusted publishing and short-lived GitHub OIDC credentials.
- Added NuGet package discovery, installation guidance, and a main-package version badge to the project README.
- Added signed Maven Central bundle publication through the Central Portal API, including automatic validation, release-status polling, and synchronized .NET/Java version checks.
- Added Maven Central artifact discovery and a coordinated tagged release procedure for keeping NuGet and Maven releases on the same commit.
- Published Java artifacts under the GitHub-verified `io.github.marinasundstrom.myservicebus` Maven group.
- Declared the sample application's fat-JAR inputs as task dependencies so the aggregate Gradle build remains valid under Gradle 9.

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
- Aligned polymorphic mediator and in-memory dispatch so concrete messages reach concrete, interface, and non-root base contracts once in both clients.

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

- Added an optional cross-language runtime-monitoring MVP with general-purpose hooks, bounded exporters, a central in-memory collector, real-time metrics and flow queries, failure inspection, replica grouping, and a themed standalone Blazor dashboard.
- Added sanitized light and dark monitoring-dashboard screenshots to the runtime-monitoring documentation and website.
- Audited the public walkthrough and sample documentation, corrected stale Java and Aspire commands, fixed preview inspection routes and broken links, and aligned product and compatibility wording with the broker-backed MVP boundary.
- Fixed the Aspire interoperability sample by aligning Aspire package versions, supervising the Java Gradle task, using dynamic external endpoints, and injecting the orchestrated RabbitMQ endpoint into every client.
- Deferred C# bus construction until hosted post-build configuration is applied, allowing Aspire's dynamically assigned RabbitMQ endpoint to take effect; also made the Java HTTP target dynamic and selected explicit-bucket metrics where supported.
- Added the existing MassTransit sample as an Aspire resource on the shared broker, with health endpoints and OpenTelemetry, so local orchestration demonstrates live C#, Java, and MassTransit interoperability.
- Declared and CI-checked the MVP runtime and interoperability baseline, including exact .NET SDK, RabbitMQ, MassTransit, Gradle, and client-library versions plus the preview support window.
- Added clean C# and Java consumer smoke projects that restore and run exclusively from the staged NuGet and Maven publications.
- Added CI package verification for the four preview NuGet packages and seven Maven publications, validating exact artifact sets plus package identity, licensing, repository, symbols, sources, and Javadocs.
- Completed the mediator and in-memory stability matrix with matching directed-send and publish fan-out scenarios, added the missing Java local APIs, and fixed duplicate C# consumer delivery by sharing one receive transport per logical endpoint.
- Added deterministic C# and Java scheduling conformance for both publish and directed send using injectable manual job schedulers, including cancellation without wall-clock sleeps and an explicit absence of same-time ordering guarantees.
- Added matching eventual consumed-type observations to the C# and Java in-memory harnesses with explicit timeouts, successful-consumer-completion cardinality, and idiomatic cancellation behavior.
- Aligned C# and Java local multiple-consumer dispatch so every matched consumer is attempted independently, dispatch waits for all deliveries, failures propagate without suppressing sibling consumers, and no inter-consumer ordering is promised.
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
