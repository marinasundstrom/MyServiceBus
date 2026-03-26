# Metrics And Event/Message Log APIs Proposal

## Status

Proposed

## Summary

This proposal defines first-party, opt-in metrics and event/message log APIs for MyServiceBus.

The feature should remain outside the core `MyServiceBus` packages and be implemented as addon packages that integrate through existing MyServiceBus extension points, primarily the pipeline/filter system.

The proposal covers:

- programmatic metrics APIs
- programmatic event/message log APIs
- optional HTTP exposure of those APIs
- optional persistence for state hydration and flush
- dashboard-oriented querying of recorded MyServiceBus events and message records

The feature is intentionally scoped to MyServiceBus-owned data. It should not attempt to become a broker management or broker inspection API.

## Goals

- Expose stable, dashboard-friendly metrics and event/message log DTOs.
- Support programmatic querying of MyServiceBus runtime state and recorded operational records.
- Keep HTTP endpoints as one way to expose these APIs, not the only way.
- Keep the feature opt-in.
- Keep the implementation transport-aware but not broker-driven.
- Persist MyServiceBus-specific operational events such as faults, retries, skipped messages, and moves to error.
- Allow dashboards and tools to query recorded events through the inspection API.
- Support conditional recording of significant MyServiceBus events and messages.
- Preserve C# and Java parity.
- Reuse existing pipeline abstractions whenever possible.

## Non-Goals

- Querying brokers directly for queue depth, dead-letter contents, connection details, or health.
- Exposing raw broker management APIs.
- Baking dashboard-specific concepts into the core bus packages.
- Making health checks part of the inspection runtime model.
- Recording every publish, send, and successful consume by default.

## Design Principles

### Addon First

Metrics and event/message log APIs should be shipped as first-party addons rather than built directly into core `MyServiceBus`.

Core MyServiceBus should stay focused on:

- messaging
- transport abstraction
- pipeline execution
- topology registration

Addons should own:

- event recording
- metrics aggregation
- HTTP exposure
- optional persistence integrations

### Programmatic First

The programmatic metrics and event/message log APIs are the primary feature.

HTTP endpoints are adapters over those APIs. A service should be able to expose this information:

- over HTTP
- in tests
- in CLI tools
- in custom dashboards
- in internal support tooling

without coupling the feature to ASP.NET or any specific web framework.

### MyServiceBus Scope Only

The metrics and event/message log APIs should expose information that MyServiceBus itself knows or decides.

Examples:

- topology registered in MyServiceBus
- message and consumer mappings
- send, publish, consume activity observed by MyServiceBus
- retries performed by MyServiceBus
- fault publications performed by MyServiceBus
- messages moved to error by MyServiceBus
- skipped or unknown-message situations observed by MyServiceBus

The feature should not expose:

- broker queue depth
- broker dead-letter state
- broker health
- broker-native connection metadata beyond what MyServiceBus already models

If users need deeper broker information, they should query their broker or other service-specific endpoints directly.

### Transport-Aware, Not Broker-Driven

The APIs may include transport-aware details when MyServiceBus itself models them, but they should not become broker administration surfaces.

## Proposed Package Layout

### .NET

- `MyServiceBus`
  Core messaging, transport, topology, pipeline
- `MyServiceBus.Inspection`
  Programmatic inspection API and endpoint discovery
- `MyServiceBus.Inspection.Metrics`
  Metrics state, event/message log recording, in-memory state, query abstractions
- `MyServiceBus.Inspection.Persistence.*`
  Optional persisted stores
- `MyServiceBus.AspNetCore.Inspection`
  HTTP endpoint mapping for inspection
- `MyServiceBus.AspNetCore.HealthChecks`
  Health check integration, separate from inspection

### Java

- `myservicebus`
  Core messaging, transport, topology, pipeline
- `myservicebus-inspection`
  Programmatic inspection API and endpoint discovery
- `myservicebus-inspection-metrics`
  Metrics state and event/message log recording
- `myservicebus-inspection-persistence-*`
  Optional persisted stores
- `myservicebus-web-inspection`
  HTTP exposure for Javalin or another chosen web framework
- `myservicebus-health`
  Health exposure, separate from inspection

## What Stays In Core

The core bus should expose only the seams required to support addons cleanly.

These are already aligned with the current design:

- send pipeline configuration
- publish pipeline configuration
- consume pipeline configuration
- retry through the pipeline
- fault handling through filters
- error transport through filters

Minimal additions to core are acceptable only when an addon cannot be built cleanly on current seams.

Likely acceptable core additions:

- a clean global consume-pipeline hook that applies to all handlers and consumers
- a skipped-message observer or equivalent pipeline seam
- a retry observer or retry callback if retry attempts cannot be observed cleanly through existing filters

Not acceptable in core:

- HTTP DTOs
- dashboard models
- persisted metrics stores
- metrics or event log query endpoints
- broker querying logic

## Inspection Exposure

Inspection remains useful as an exposure and discovery surface, but it is not the primary feature described by this proposal.

Its role here is to expose:

- overview and capability discovery
- metrics state
- event/message log query endpoints

The metrics and event/message log APIs must also be usable programmatically without HTTP.

## Health

Health should be separate from inspection.

Recommended endpoints:

- `/health/live`
- `/health/ready`

The inspection overview may include:

- `readyUrl`
- `liveUrl`
- optional cached `status`

The inspection API must not actively execute health checks or query the broker for health.

If `status` is present in inspection, it must be derived from already-known in-process runtime state, not from a fresh health probe.

## Metrics State And Recorded Events

### Why Metrics Are Optional

Topology and inspection data are deterministic and configuration-based.

Metrics state and recorded events have different semantics:

- they can be instance-local
- they can be durable
- they can be filtered
- they may have retention and storage costs

Because of that, metrics state and recorded-event capture should be an opt-in addon feature.

### Runtime Model

Metrics state should always exist in memory at runtime.

Default behavior:

- aggregate state is maintained in memory for the lifetime of the service instance
- aggregate state is immediately available for live inspection queries
- aggregate state resets on restart if persistence is not configured

Persistence is an optional addon capability layered on top of that in-memory runtime model.

It should not replace the live in-memory state. Instead, it should:

- hydrate selected aggregate state on startup
- flush aggregate state in batches at configurable intervals
- flush aggregate state when the service is stopping
- optionally flush aggregate state when the service fails
- persist buffered message records and bus events through a separate batched recording pipeline

This behavior should be controlled by configuration parameters, not inferred hidden state.

### Semantics

Without persistence:

- aggregate state is instance-local
- recorded messages and bus events are available only if explicitly stored in memory by the addon
- data resets on restart
- suitable for development, testing, and live local dashboards

With persistence enabled:

- the in-memory state remains the live source for inspection
- selected aggregate state can survive restarts
- recorded messages and bus events can be stored durably
- persisted records support traceability and dashboard history
- durability and sharing semantics depend on the configured backend and flush strategy

The inspection overview should make this explicit when it exposes metrics capabilities.

Example:

```json
{
  "metrics": {
    "enabled": true,
    "mode": "in-memory",
    "persistence": "enabled",
    "scope": "instance",
    "metricsUrl": "/inspection/v1/metrics",
    "eventsUrl": "/inspection/v1/events"
  }
}
```

Recommended persistence parameters:

- `hydrateOnStartup`
- `flushInterval`
- `flushOnShutdown`
- `flushOnFailure`
- `maxBatchSize`
- `maxBufferSize`
- `provider`

Recommended distinction in the model:

- `state`
  Aggregate counters and summary values such as failed message counts, retry counts, moved-to-error counts, and skipped counts.
- `records`
  Stored message records and bus event records used for traceability and dashboard drill-down.

## What Should Be Recorded

The recording system should persist only MyServiceBus-specific operational events and significant message records.

This is separate from aggregate state such as the number of failed messages.

Captured records should be buffered in memory and flushed in batches. Persistence should not perform write-through storage on every capture by default.

Recommended default recorded event types:

- `consume_faulted`
- `retry_attempted`
- `retry_exhausted`
- `fault_published`
- `moved_to_error`
- `message_skipped`

Optional event types:

- `published`
- `sent`
- `consumed`
- `message_recorded`

The defaults should favor signal over volume.

## Recorded Events And Message Records

### Purpose

Recorded events and message records exist to make MyServiceBus behavior traceable.

Examples:

- why did this consumer fail
- how many retries happened
- which messages were moved to error
- which messages were skipped
- when was a fault published

This is distinct from broker-level truth.

It is also distinct from application logging.

Structured logs remain a separate concern. The event-history feature is a queryable MyServiceBus operational record, not a replacement for logs, traces, or log sinks.

### Event Model

The stored record model should be generic enough to represent:

- bus operational events
- significant message records

Suggested fields:

- `occurredAtUtc`
- `category`
- `eventType`
- `serviceName`
- `instanceId`
- `transportName`
- `queueName`
- `consumerType`
- `messageType`
- `messageUrn`
- `messageId`
- `correlationId`
- `conversationId`
- `attempt`
- `exceptionType`
- `exceptionMessage`
- `matchedPolicy`
- `properties`

### Categories

- `BusEvent`
- `SignificantMessage`

### Query Surface

The inspection API should be able to query recorded bus events and stored message records for dashboards.

Recommended endpoint:

- `/inspection/v1/events`

Suggested filters:

- `category`
- `eventType`
- `messageType`
- `messageId`
- `correlationId`
- `queueName`
- `consumerType`
- `from`
- `to`
- `limit`
- `cursor`

## Conditional Recording

The recording system should support conditional recording policies.

This is required to:

- keep storage volume under control
- focus dashboards on meaningful events
- allow audit-like recording of specific messages or consumers

Examples:

- record all faults
- record all retries
- record all terminal failures
- record all messages of type `SubmitOrder`
- record all messages handled by `PaymentConsumer`
- record only messages matching a custom predicate

Suggested policy types:

- event-type policy
- message-type policy
- consumer-type policy
- predicate policy

## Metrics State Snapshot

The metrics API should expose aggregate state derived from the configured in-memory state, optionally hydrated and flushed through the configured persistence layer.

Recommended aggregate fields:

- `published`
- `sent`
- `consumed`
- `faulted`
- `retried`
- `retriedMessages`
- `terminalFailures`
- `movedToError`
- `publishedFaults`
- `skipped`

These are MyServiceBus semantics, not broker semantics.

For example:

- `terminalFailures` means MyServiceBus exhausted its processing attempts or classified the message as terminal
- `movedToError` means MyServiceBus moved the message to its configured error path

The API should avoid vague or broker-specific labels such as `dead`.

This endpoint is intentionally about state, not individual stored records.

## Implementation Approach

### Use Existing Pipeline Seams

The feature should be built primarily using the current pipeline/filter model.

That means:

- send filters for sent events
- publish filters for publish events
- consume filters for consume, fault, and timing events
- retry filters or retry observers for retry events
- error transport filters for move-to-error events

This keeps the design aligned with MyServiceBus extensibility goals and avoids special-casing inspection inside core runtime logic.

### Addon Registration API

The addon should expose high-level registration helpers rather than making users assemble filters manually.

Examples:

```csharp
builder.Services.AddServiceBus(x =>
{
    x.UseInMemoryMetrics(options =>
    {
        options.RecordFaults();
        options.RecordRetries();
        options.RecordSkipped();
        options.RecordTerminalFailures();
        options.RecordMessagesOfType<SubmitOrder>();
    });
});
```

```csharp
builder.Services.AddServiceBus(x =>
{
    x.UsePersistedMetrics(options =>
    {
        options.RecordFaults();
        options.RecordRetries();
        options.RecordTerminalFailures();
        options.HydrateOnStartup = true;
        options.FlushInterval = TimeSpan.FromSeconds(30);
        options.FlushOnShutdown = true;
        options.FlushOnFailure = true;
    });
});
```

Equivalent Java APIs should follow the same conceptual shape.

### Internal Structure

The addon implementation should be layered as follows:

1. candidate event creation in filters and observers
2. policy evaluation
3. in-memory aggregate state update
4. in-memory buffering of recorded events and message records
5. optional batched persistence flush
6. optional state hydration
7. aggregate/query provider
8. inspection DTO mapping
9. optional HTTP endpoint exposure

Filters should emit candidate events.

The recorder or policy layer should decide whether those events are recorded.

The in-memory aggregate state should remain the live runtime source used by the metrics API and any inspection adapter over it.

Persistence should hydrate and flush that state rather than bypass it.

Recorded messages and bus events should remain a separate queryable store or recording stream.

Record capture should be cheap on the hot path:

- update aggregate state immediately in memory
- append records to an in-memory buffer
- flush persisted state and records in batches
- avoid per-capture persistence writes by default

This keeps filtering logic centralized and makes conditional recording consistent.

## Programmatic Interfaces

The exact API shape can evolve, but the design should include the following concepts.

### Metrics State And Event/Message Logs

- `IBusMetricsStore`
- `IBusMetricsRecorder`
- `IBusMetricsProvider`
- `IBusMetricsPersistence`
- `IBusEventRecordingPolicy`
- `BusEventRecordContext`
- `BusMetricEvent`
- `BusMetricsSnapshot`
- `BusMetricsQuery`
- `BusMetricsQueryResult`

Recommended split:

- aggregate state provider and persistence abstractions
- recorded-event and stored-message recorder/query abstractions
- optional inspection adapter abstractions

### HTTP Adapters

- ASP.NET endpoint mapping extensions
- Java web route mapping helpers

## DTO Guidance

Metrics and event/message log DTOs should follow these rules:

- use strings for type identity
- no CLR or JVM runtime type serialization
- use arrays and objects rather than framework-specific metadata
- prefer `[]` and `{}` over `null` for empty collections and maps
- use `null` only when data is meaningfully unknown or unavailable
- include timestamps in UTC
- make transport-specific details additive under nested properties

## Rollout Plan

### Phase 1

- finalize addon package direction
- keep the existing sample app endpoints as validation targets
- expose health separately and reference it only as discovery metadata where needed

### Phase 2

- add metrics addon abstractions
- add in-memory metrics/event store
- add `/inspection/v1/metrics`
- add `/inspection/v1/events`
- support query filters over recorded events

### Phase 3

- add persistence abstractions and first implementation
- support hydration of selected metrics and events on startup
- support configurable flush intervals
- support configurable batch sizes and buffer sizes
- support flush on shutdown and failure
- make event history durable
- document persistence parameters and semantics clearly

### Phase 4

- refine conditional recording policies
- add paging and retention guidance
- add dashboard-focused examples and queries

## Open Questions

- Should the inspection snapshot provider remain in core temporarily while addon packages are formed, or should it move immediately.
- What is the smallest acceptable core addition needed to observe skipped messages cleanly.
- Whether retry observation can be implemented entirely through existing pipeline filters or needs a dedicated observer seam.
- Which persisted store should be the first supported implementation.
- Whether significant-message recording should support explicit manual recording APIs in addition to automatic policy-based capture.

## Recommendation

Proceed with a first-party addon architecture.

Keep core `MyServiceBus` focused on messaging and pipeline extensibility.

Use the existing pipeline and topology seams as the primary implementation mechanism.

Add only the smallest missing hooks to core when the addon cannot observe a MyServiceBus-owned event cleanly.

Treat metrics state and event/message logs as the primary read models.

Treat HTTP endpoints, including inspection endpoints, as optional adapters over those read models.

Treat the whole feature as an opt-in subsystem with explicit in-memory and persistence semantics.
