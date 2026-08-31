# Monitoring and Control Backlog

This document records product and architecture ideas for MyServiceBus monitoring. It is an **unprioritized inventory**. A later planning pass should order the entries by user value and architectural dependency rather than treating their order here as a delivery sequence.

## Direction already decided

- The supplied dashboard is application-centric. A distributed application and its participating services are the primary objects; buses, endpoints, topology, transports, and brokers are progressively disclosed when they explain application behavior.
- The dashboard serves application development and operational monitoring. Its landing page remains a concise overview, while focused views carry technical detail.
- The dashboard is a consumer. MyServiceBus collection, aggregation, query, and monitoring-history persistence belong to the monitoring service. External telemetry and broker information use explicit provider boundaries. Privileged operations require a separate control-plane boundary.
- C# and Java runtime behavior should remain aligned for core concepts, primitives, and transports. Familiarity with MassTransit should ease interoperability and transition, but MyServiceBus does not copy its API or inherit its legacy constraints.
- Graphs and maps are a recurring visual language. Compact dashboard charts and full Metrics or Flow views should reuse distinct components, support streamed updates, and remain usable on mobile.
- Dashboard-specific configuration belongs in `appsettings.json`. A future JSON layout may arrange stable widget identities, but user customization is intentionally deferred until the default information hierarchy matures.

## Scheduling and jobs

One-time scheduled messages and recurring jobs are separate concepts.

- **Scheduled messages** have one due time and a schedule/cancel lifecycle. The current monitoring model reads bounded state from the shared in-memory source or the authoritative PostgreSQL outbox source.
- **Recurring jobs** have their own identity, cadence, next occurrence, pause/resume state, misfire policy, and execution history. They should not be represented as scheduled messages with an extra flag.
- The first recurring-job slice may authoritatively report definition, occurrence creation, and command dispatch. It must not report application completion until a later job-execution layer supplies that evidence.
- A future `JobConsumer`-style abstraction may add long-running-work conveniences such as progress, retry, cancellation, and execution status. It is not a prerequisite for the scheduling primitives.
- The outbox dispatcher is mechanically a recurring background loop, but semantically it remains runtime infrastructure. It is not a user-defined recurring job and should not appear in the recurring-job API or view. The two may eventually share internal timing or lease primitives without sharing their public model.
- Scheduler management—cancel, retry, reschedule, pause, or resume—is future control-plane work. Read-only visibility does not authorize those actions.

## Future read models

- Recurring-job definitions, next occurrences, execution history, and status.
- Saga state distribution, transitions, faults, and correlations, with only an actionable summary on the landing page.
- Broker identity, connection health, queue depth, dead-letter state, and selected broker or host CPU and memory signals. Broker credentials and least-privilege access are configured separately from MyServiceBus monitoring export.
- Longer historical ranges and aggregates beyond the active monitoring window.
- A possible engineering-focused dashboard organized around buses, topology, endpoints, and broker objects. It can consume the same precise contracts without changing the supplied application-centric dashboard.

## Alerts

Alert evaluation should be shared across MyServiceBus observations, scheduler state, broker state, and other configured sources instead of being reimplemented by every provider.

- Keep MyServiceBus collection focused on MyServiceBus data.
- Evaluate cross-source thresholds and warning caps in a separate alerting service or similarly explicit boundary.
- Expose alert state through the monitoring query plane for the dashboard.
- Add notification providers independently; email can be exercised against a mock SMTP server.
- Define freshness, missing-data, deduplication, suppression, recovery, and audit semantics before treating alerts as production-ready.

## Privileged controls

The dashboard may eventually host controls, but the browser must not call scheduler stores or broker administration APIs as if they were ordinary monitoring queries.

- Scheduler commands depend on the selected scheduler provider.
- Purge, delete, replay, dead-letter, queue reset, and similar commands depend on transport capabilities.
- Every destructive or state-changing command needs capability discovery, authorization, confirmation, audit records, idempotency where possible, and explicit partial-failure behavior.
- Monitoring credentials do not imply control-plane credentials.

## Prioritization method

The later prioritization pass should score each slice on two independent axes.

### User and operational value

- How quickly does it help an application developer understand a distributed application?
- Does it shorten detection, diagnosis, or recovery for a likely operational problem?
- Is it understandable to users who are new to asynchronous messaging?
- Does it benefit both development and deployed monitoring?
- How many languages, transports, and providers benefit?

### Architectural order and risk

- Is a runtime primitive or transport capability required first?
- Does it need a new provider contract, monitoring read model, or persistence model?
- Is the feature read-only presentation, cross-source aggregation, or a privileged command?
- What authentication, authorization, audit, privacy, retention, and failure semantics must exist first?
- Can it be delivered as a small independently testable C# and Java slice?

Value determines why a feature matters; architectural order determines when it can be delivered responsibly. The resulting plan should identify prerequisite decisions separately from user-visible slices and preserve separate feature branches, commits, and merge commits.
