# Runtime Monitoring Proposal

## Status

Experimental MVP implemented and validated end to end through the Aspire stack. The proof of concept covers general-purpose hooks, C# and Java exporters, retry observations, bounded time-window and time-series metrics, observed flow reconstruction, in-memory query APIs, WebSocket invalidations, and a standalone grouped Blazor overview with light and dark themes. Authentication, payload-byte limits, persistence, broker and host metrics, automated scaling advice, and external telemetry links remain future work. See [Runtime Monitoring](../runtime-monitoring.md) for setup and the current operational boundary.

## Recommendation

Build MyServiceBus monitoring around a small central service:

```text
MyServiceBus client
  -> monitoring exporter
  -> MyServiceBus monitoring service
  -> query API
  -> standalone Blazor dashboard
```

The monitored applications instrument bus activity and export it asynchronously. They do not own monitoring history and do not serve query APIs. The monitoring service collects data from every application, maintains the distributed runtime model, and owns all aggregation and future persistence. The dashboard only queries and subscribes to the monitoring service.

This should be a fully optional addon. A MyServiceBus application works normally without the exporter, collector, dashboard, or a database.

The implementation should own only the MyServiceBus-specific integration and runtime model. It should reuse existing hosting, transport, authentication, serialization, live-update, telemetry, and database technology rather than recreate those systems.

## Design Boundary

Build:

- client hooks for immutable MyServiceBus model and lifecycle events
- an exporter hook that batches those events
- a central service that accepts, aggregates, stores, and queries MyServiceBus monitoring data
- a standalone dashboard that consumes the service APIs
- optional dashboard providers that link to existing telemetry systems

Reuse:

- HTTP and JSON for ingestion and queries
- WebSocket libraries for live dashboard subscriptions
- host authentication and authorization
- existing OpenTelemetry instrumentation and external telemetry backends
- existing transport management tools
- established database providers when persistence is added

Do not build:

- a generic telemetry collector
- a trace, log, or infrastructure-metrics backend
- broker administration APIs
- a custom authentication system
- a custom database engine

The monitoring service is a domain-specific read-model service, not a new observability platform.

## Naming And MassTransit Alignment

Use familiar MassTransit terminology where the concepts match, without copying its API or deployment model.

MassTransit currently groups this area under **Monitoring and Observability** and uses **observers**, **bus topology**, **receive endpoints**, **metrics**, **flow**, and **dashboard**. Its dashboard describes topology and runtime state. Those are good shared domain terms.

Recommended product names:

- **MyServiceBus Monitoring**: the complete optional capability
- **MyServiceBus monitoring exporter**: the client-side addon
- **MyServiceBus monitoring service**: the collector and query backend
- **MyServiceBus Dashboard**: the standalone Blazor UI
- **hook**: an opt-in handler invoked for a specific immutable bus event
- **bus metadata**: application, bus, topology, endpoint, consumer, and transport description
- **observation**: a live bus or message lifecycle notification
- **metrics**: aggregates derived by the monitoring service from MyServiceBus observations
- **message flow**: observed relationships between applications, endpoints, consumers, and message types

Avoid `introspection` as the main product name. It is accurate for examining one process, but `monitoring` better describes a central service observing a distributed system over time.

The existing `MyServiceBus.Inspection` and `myservicebus-inspection` work should become the bus-metadata foundation. Since those packages are still preview-only, fold them into the monitoring package family before publication rather than creating overlapping inspection and monitoring concepts.

MassTransit compatibility is not a requirement. Alignment means recognizable messaging vocabulary and comparable semantics, not compatible endpoints, DTOs, packages, or UI behavior.

## Goals

- Monitor any C# or Java application that uses a MyServiceBus client.
- Let applications self-register with a central monitoring service by exporting metadata and heartbeats.
- Show applications, instances, buses, endpoints, consumers, handlers, and message types.
- Show send, publish, receive, consume, retry, and failure throughput.
- Show recent retries, exhausted retries, faults, skipped messages, and moves to error.
- Reconstruct message flows across distributed applications.
- Preserve optional W3C trace correlation so dashboard integrations can find related telemetry.
- Keep client overhead bounded and off the message-processing critical path.
- Keep aggregation, query, retention, and persistence in the monitoring service.
- Preserve equivalent C# and Java behavior and wire DTOs.
- Let transport packages contribute authoritative transport metadata.

## Non-Goals

- Application-local monitoring databases or history query APIs.
- MassTransit API, protocol, dashboard, or wire compatibility for monitoring.
- Broker administration, queue browsing, requeue, purge, or dead-letter operations.
- Guaranteed audit delivery.
- Message-body or arbitrary-header capture.
- Receiving, storing, proxying, or querying OpenTelemetry data in the monitoring service.
- Alerting in the first version.
- Multiple coordinated monitoring-service replicas in the first version.
- A production dashboard in the first version.

## Architecture

### 1. Client instrumentation

Core MyServiceBus exposes only the portable facts and hook seams needed by observability tooling:

- normalized topology snapshots
- application, bus, endpoint, consumer, handler, and message identities
- immutable hook events for bus and receive-endpoint lifecycle changes
- immutable hook events for receive, consume, send, and publish activity
- immutable hook events for retries, exhausted retries, skipped messages, and moves to error
- optional W3C trace correlation already present in message contexts

These programmatic APIs are useful to tests and other tools even when monitoring is disabled. Core does not know the monitoring-service address, open network connections, aggregate monitoring windows, or persist data.

Core should add only the smallest missing hook seams required for reliable coverage. A hook must never be able to change a message or alter its outcome; filters remain the interception mechanism.

#### Hook contract

`Hook` is the working name for the extension point. The conceptual shape is a typed handler:

```csharp
public interface IBusHook<in TEvent>
    where TEvent : IBusHookEvent
{
    void Handle(TEvent busEvent);
}
```

```java
public interface BusHook<TEvent extends BusHookEvent> {
    void handle(TEvent busEvent);
}
```

The exact public names can change, but the semantics should not:

- events are immutable and contain metadata, never mutable pipeline contexts
- hooks are optional and have zero effect on behavior when none are registered
- hook exceptions are isolated, logged, and never propagated into messaging
- hook execution is fast and non-blocking
- hook order is not a business contract
- hooks must not perform remote I/O directly
- high-volume event allocation should be avoided when no hook or telemetry listener is active

Initial typed events should include:

- `BusModelChanged`: a new immutable metadata revision is available
- `BusLifecycleChanged`
- `ReceiveEndpointLifecycleChanged`
- `MessageSent` and `MessageSendFaulted`
- `MessagePublished` and `MessagePublishFaulted`
- `MessageReceived` and `MessageReceiveFaulted`
- `MessageConsumed` and `MessageConsumeFaulted`
- `MessageRetryAttempted` and `MessageRetryExhausted`
- `FaultPublished`
- `MessageMovedToError`
- `MessageSkipped`

The model-change hook carries a stable revision and reason. The exporter then serializes the corresponding immutable metadata snapshot. Runtime hooks carry identity, timing, result, and trace correlation but not message bodies.

Telemetry instrumentation and the monitoring exporter may use the same hook events independently. Neither integration depends on the other. Other addons and tests may implement hooks without coupling core to a collector protocol.

### 2. Transport metadata contributors

Transport packages extend bus metadata with authoritative details they already model, such as entity names, logical addresses, bindings, and capability flags.

Transport metadata is additive and namespaced. Generic monitoring code must not infer broker state. Queue depth, broker health, dead-letter contents, and credentials are not part of the portable model.

### 3. Monitoring exporter

The optional exporter registers hook handlers. Those handlers only copy events into a bounded in-memory channel. A background worker batches the events and calls the monitoring service.

It:

- sends a full metadata snapshot on startup and whenever configuration changes
- renews an instance registration lease with heartbeats
- batches lifecycle and message observations
- carries MyServiceBus application identity and optional W3C trace correlation
- retries transient failures with bounded exponential backoff
- uses a bounded in-memory queue
- reports dropped observations in the next successful batch
- flushes for a short bounded interval during graceful shutdown

It does not:

- store queryable history
- expose a monitoring HTTP server
- calculate long-lived rates
- merge instances
- persist retry batches to disk by default
- block messaging while the monitoring service is slow or unavailable

If the export queue is full, monitoring data is dropped. Messaging continues. The service records the reported gap so operators can distinguish zero traffic from incomplete coverage.

#### Batching and cadence

Observation export is triggered by whichever condition occurs first:

- the configured flush interval elapses
- the maximum observation count is reached
- the maximum serialized payload size is reached
- the application begins graceful shutdown

Recommended prototype defaults:

- `exportInterval`: 1 second
- `maxBatchSize`: 256 observations
- `maxBatchBytes`: 256 KiB
- `maxQueueSize`: 10,000 observations
- `heartbeatInterval`: 15 seconds
- `leaseTimeout`: 45 seconds
- `shutdownFlushTimeout`: 2 seconds
- `metadataChangeDebounce`: 500 milliseconds

These are exporter configuration values with corresponding C# and Java semantics, not fixed protocol constants. The monitoring service may advertise smaller accepted limits, but it must not silently increase the client's configured resource usage.

Metadata is sent immediately at startup. Closely spaced model changes are debounced and coalesced into the latest immutable revision. Heartbeats are independent from observation batches so an idle application remains visible without manufacturing activity.

Failures, retries, and other significant events use the same batch path. They do not bypass the queue with synchronous HTTP calls. This preserves the rule that monitoring cannot add network latency to message processing.

After a failed export, the worker retains batches only within the configured queue bound and retries with exponential backoff and jitter. The next accepted batch reports any dropped count and sequence gap. A successful export resets the backoff.

### 4. Monitoring service

The monitoring service is both collector and query backend. All expensive work—validation, deduplication, aggregation, flow reconstruction, retention, querying, and persistence—runs here rather than in monitored applications.

Its internal boundaries are:

- `MonitoringReceiver`: authenticated ingest API
- `ApplicationRegistry`: applications, instances, leases, and connectivity
- `MetadataRepository`: current and previous bus metadata
- `ObservationIngestor`: validates, deduplicates, and sequences batches
- `MetricsAggregator`: bounded time-window aggregation
- `MessageFlowBuilder`: configured and observed graph construction
- `MonitoringQueryService`: stable programmatic query API
- `MonitoringStore`: optional persistence abstraction

The first implementation hosts these backend components in one ASP.NET Core monitoring-service process. The dashboard remains a separate application and communicates only through the query API.

The service is the only owner of distributed totals and monitoring history. It groups replicas by application identity while preserving instance identity internally for leases, restarts, gaps, and deduplication.

### 5. Dashboard

The standalone Blazor dashboard is a consumer of the query API.

It does not connect to monitored applications or own the canonical monitoring state. It may cache view state in the browser, but the monitoring service remains authoritative. Separately configured dashboard integrations may query external telemetry systems for enrichment.

The same query API can later support a CLI, tests, other dashboards, or support tooling.

## Identity And Registration

### Resource Identity

Use a small MyServiceBus resource identity:

- `applicationName`
- `instanceId`
- `applicationVersion`
- MyServiceBus client language and version
- stable `busId` within the process
- optional bounded resource `labels`

Applications configure these values through the monitoring addon. `applicationName` is the logical identity that groups worker replicas, `instanceId` identifies each running replica, and `busId` distinguishes a bus within that process. Transport addresses are descriptive metadata rather than unique application identity. For convenience, platform adapters may use common host or telemetry resource values as defaults, but the monitoring contract does not depend on an OpenTelemetry SDK.

### Self-Registration

An application registers by sending its first metadata snapshot. No separate administrative registration call is required.

Each instance receives or supplies a registration identifier and renews a lease through heartbeats. If the lease expires, the monitoring service marks that instance offline but retains its last metadata according to retention policy.

Clients may attach a small bounded set of resource labels. `group` is a conventional label for arranging related logical applications, while labels such as `environment` and `role` remain generic query and display metadata. The hierarchy is `label group → application → instances`: applications group their replicas automatically, and labels never replace runtime identity. Advanced dashboard-defined grouping rules and stored operator annotations remain future work.

Static allowlists or expected-application definitions may be added later to show missing applications and reject unknown ones.

## Data Model

### Bus Metadata Snapshot

A versioned snapshot contains:

- resource identity
- protocol version and capabilities
- bus address, lifecycle state, and transport
- message types and message URNs
- receive endpoints and logical addresses
- bindings
- consumers and handlers
- relevant immutable pipeline descriptors
- optional transport-contributed details
- capture time and metadata revision

Metadata describes current configuration. A new snapshot supersedes the previous snapshot for the same instance and bus. The monitoring service may persist revisions later.

### Observation Envelope

Each observation contains:

- process-local `sequence`
- `occurredAtUtc`
- `kind`
- application, instance, and bus identity
- endpoint and destination identity when applicable
- message, consumer, and handler type identity when applicable
- duration when known
- retry attempt when applicable
- exception type and truncated message when applicable
- message, correlation, conversation, trace, and span identifiers when available
- additive properties

Initial observation kinds:

- `bus_started`
- `bus_stopped`
- `endpoint_ready`
- `endpoint_stopped`
- `sent`
- `send_faulted`
- `published`
- `publish_faulted`
- `received`
- `receive_faulted`
- `consumed`
- `consume_faulted`
- `retry_attempted`
- `retry_exhausted`
- `fault_published`
- `moved_to_error`
- `message_skipped`

Successful high-volume observations may be sampled or aggregated in the exporter later. Retry and terminal-failure observations should be enabled by default.

### Batch Envelope

The exporter sends batches with:

- protocol version
- resource and bus identity
- batch identifier
- first and last observation sequence
- dropped-observation count since the previous accepted batch
- export timestamp
- observations

The receiver uses the batch identifier and sequence range for deduplication and gap detection. Acceptance is at-least-once at batch level when retries occur; queries and aggregates must not double-count a retried batch.

### Metrics

The monitoring service derives sliding-window metrics from observations:

- sent and send faults
- published and publish faults
- received and receive faults
- consumed and consume faults
- retry attempts and exhausted retries
- faults published
- moved to error
- skipped messages
- active instances and endpoints
- operation durations and percentiles where supported

Dimensions are bounded to application, instance, bus, endpoint, message type, consumer type, handler type, and exception type.

Message IDs, trace IDs, correlation IDs, conversation IDs, and exception messages must never become metric dimensions.

### Message Flow

The monitoring service builds two graphs:

- **configured topology**: possible routes derived from metadata
- **observed flow**: actual producer-to-consumer relationships derived from observations and correlation

Configured and observed edges remain distinct. Flow aggregation is bounded and never creates a permanent graph node per message.

## Protocol

### Direction

Applications initiate outbound connections to the monitoring service. This works for web apps, workers, containers, and services behind firewalls without adding an inbound monitoring port to every application.

The dashboard only connects to the monitoring service.

### Ingest API

Start with versioned HTTP and JSON because it is easy to implement and validate in both C# and Java.

Suggested endpoints:

- `POST /api/monitoring/v1/metadata`
- `POST /api/monitoring/v1/observations:batch`
- `POST /api/monitoring/v1/heartbeat`
- `POST /api/monitoring/v1/instances/{instanceId}:stopped`

Responses communicate acceptance, server time, lease duration, and optional exporter guidance such as the next heartbeat interval or maximum batch size.

The protocol is a MyServiceBus-owned ingest protocol. It does not accept OTLP. Bus topology and MyServiceBus lifecycle events do not map cleanly to standard telemetry signals, and combining the protocols would blur their ownership and retention semantics.

Protobuf and gRPC may be added after the JSON contract is stable and profiling proves a need.

### Query API

The dashboard uses a separate read-only API:

- `GET /api/monitoring/v1/applications`
- `GET /api/monitoring/v1/applications/{application}/instances`
- `GET /api/monitoring/v1/instances/{instanceId}/metadata`
- `GET /api/monitoring/v1/metrics`
- `GET /api/monitoring/v1/metrics/timeseries`
- `GET /api/monitoring/v1/observations`
- `GET /api/monitoring/v1/flow`

Metric and observation queries support bounded time ranges, filters, cursor pagination, and explicit completeness/gap metadata.

### Live Dashboard Updates

The HTTP query API is authoritative. Add a versioned WebSocket API for subscriptions to application, metric, observation, and flow changes:

- `GET /api/monitoring/v1/stream`

WebSocket is preferable as the public live protocol when any dashboard should be able to consume it. Clients can send bounded subscription filters, and the service can send invalidations or deltas. A client always recovers by querying a fresh HTTP snapshot after reconnecting or detecting a sequence gap.

SignalR may be an additional adapter for the Blazor implementation, but it should not be the only public live API. Neither WebSocket nor SignalR is part of the C# and Java exporter protocol.

## Telemetry Integrations

Monitoring and telemetry are separate capabilities.

The monitoring service receives only MyServiceBus metadata, hook observations, heartbeats, and exporter status. It does not:

- accept OTLP
- store spans, logs, or external metrics
- query trace or metric backends
- require an OpenTelemetry SDK in monitored applications

MyServiceBus may continue providing its existing OpenTelemetry instrumentation independently. Applications export those signals directly to the telemetry backend of their choice.

Monitoring observations may contain optional `traceId` and `spanId` fields copied from W3C trace context. These are correlation references, not embedded telemetry. Monitoring works fully when both are absent.

The dashboard defines separate integration points:

- `TelemetryLinkProvider`: builds an external link for a trace, service, or time range
- `TraceQueryProvider`: optionally retrieves a trace summary from a configured backend
- `ExternalMetricsProvider`: optionally retrieves complementary application or infrastructure metrics

Provider implementations can target systems such as Aspire, Jaeger, Grafana Tempo, Application Insights, or another OpenTelemetry-compatible backend. Provider credentials and queries stay in the dashboard host. The monitoring service remains unaware of them.

The first prototype should implement only configurable trace-link templates. Rich backend queries should be added one provider at a time after the dashboard establishes a concrete need.

## Storage

### Initial In-Memory Store

The monitoring service initially keeps:

- active and recently expired registration leases
- the latest metadata snapshot per bus instance
- bounded metric time windows
- bounded recent significant observations
- bounded configured and observed flow graphs

Restarting the monitoring service resets this data in the first prototype.

### Future Persistence

A monitoring-service `MonitoringStore` may later persist:

- application and operator annotations
- instance sessions and lease history
- metadata revisions
- time-bucketed metrics
- selected significant observations
- flow aggregates

Persist aggregates for throughput and significant observations for diagnosis. Do not persist every successful message operation by default.

Storage batching, retention, privacy, migrations, and database selection all belong to the monitoring service. Clients remain free of monitoring persistence.

## Reliability Semantics

Monitoring is best-effort observability, not an audit log.

- The exporter never blocks a messaging operation on network I/O.
- Its in-memory queue and batches have explicit bounds.
- Export retries are bounded by time and queue capacity.
- Dropped data is reported when connectivity returns.
- The receiver deduplicates retried batches.
- Queries disclose sequence gaps and incomplete intervals.
- Graceful shutdown attempts a short flush without delaying shutdown indefinitely.

If a user needs guaranteed operational records, those should be written through a separate durable application or telemetry pipeline explicitly designed for that guarantee.

## Security And Privacy

The ingest and query APIs expose sensitive architecture and operational data.

The design should support:

- TLS for all non-local traffic
- separate ingest and query credentials
- per-application authentication or API keys initially
- scoped dashboard/operator authorization
- redaction of credentials and secrets in addresses and properties
- exception-message truncation
- bounded payload, batch, and query sizes
- no message bodies or arbitrary headers by default

The monitoring service must validate client-supplied identities rather than allowing one credential to impersonate any application unintentionally.

## Package And Project Direction

### .NET client

- `MyServiceBus`: metadata snapshots, hook seams, and existing telemetry instrumentation
- `MyServiceBus.Monitoring`: protocol DTOs, exporter, batching, and configuration

### Java client

- `myservicebus`: corresponding metadata, hook seams, and existing telemetry instrumentation
- `myservicebus-monitoring`: corresponding exporter and protocol DTOs

### Server

- `MyServiceBus.Monitoring.Server`: ASP.NET Core collector and query API
- `MyServiceBus.Monitoring.Persistence.*`: future server-side stores
- `MyServiceBus.Dashboard`: standalone Blazor sandbox
- `MyServiceBus.Dashboard.Telemetry.*`: optional external telemetry providers

The monitoring service and dashboard should be separate executable projects. The Aspire sandbox can launch them together with sample applications for a one-command development experience.

The existing inspection modules should be folded into the client monitoring packages, while any broadly useful topology snapshot APIs remain in core.

## Blazor Dashboard Scope

The sandbox prototype should show:

- discovered applications and online/offline instances
- bus, endpoint, consumer, handler, and message metadata
- live send, publish, receive, consume, retry, and failure rates
- bounded recent retries and failures
- configured and observed message flows
- monitoring gaps
- links to traces through a configured dashboard telemetry provider

It should not initially include broker actions, alerting, complete trace viewing, message bodies, automatic discovery integrations, or authentication administration.

## Rollout

### Phase 1: Contracts

- settle names, identity precedence, and versioned DTOs
- adapt the existing C# and Java inspection snapshots into matching bus metadata
- define observation, batch, lease, gap, and capability semantics
- add shared JSON conformance fixtures

### Phase 2: Exporters

- add or reuse corresponding hook seams
- implement bounded background batching in both clients
- capture retry, failure, error, skipped, trace, and span details
- verify exporter failures never affect messaging

### Phase 3: Monitoring Service

- implement metadata, observation-batch, heartbeat, and stopped ingestion
- implement leases, deduplication, gaps, in-memory metrics, and flow
- expose the read-only query API
- add authentication and protocol conformance tests

### Phase 4: Dashboard Sandbox

- build the standalone Blazor UI against the query API
- show applications, instances, topology, metrics, flow, failures, and trace links
- use the WebSocket API for live UI refresh with HTTP query recovery

### Phase 5: Persistence

- derive historical queries from the working dashboard
- add one server-side store
- define batching, retention, privacy, and migration behavior

## Decisions

- Call the overall feature `Monitoring` and the UI `Dashboard`.
- Use a small central service and MyServiceBus protocol for bus metadata and observations.
- Own only the MyServiceBus-specific runtime model and reuse established infrastructure around it.
- Have applications self-register by exporting metadata and heartbeats.
- Keep clients free of queryable monitoring history and databases.
- Put aggregation, flow reconstruction, querying, and persistence in the monitoring service.
- Keep the Blazor dashboard separate and read-only against the query API.
- Ship the monitoring service and dashboard as separate applications, orchestrated together in the sandbox.
- Keep OpenTelemetry collection and storage separate from the monitoring service.
- Add optional external telemetry providers to the dashboard.
- Start with batched HTTP JSON; add gRPC only when justified.
- Expose HTTP queries plus a portable WebSocket subscription API from the monitoring service.
- Keep SignalR optional as a Blazor-specific adapter.
- Align MassTransit terminology, not its APIs or embedded-dashboard architecture.
- Keep broker administration out of the initial scope.

## Open Questions

- Should successful send, publish, receive, and consume observations be exported individually by default or aggregated client-side after profiling?
- Which application authentication mechanism should the sample use?
- How long should an instance lease and offline-retention window be?
- Which trace-link provider should the sandbox demonstrate first?

## Sources For Terminology

- [MassTransit Monitoring and Observability](https://masstransit.massient.com/configuration/observability/)
- [MassTransit Dashboard Web UI](https://masstransit.massient.com/configuration/dashboard/)
- [MassTransit middleware probes](https://masstransit.io/documentation/configuration/middleware)
