# Transactional Outbox and Inbox Specification

## Status and Scope

This specification defines the portable transaction and failure boundary for MyServiceBus outbox and inbox persistence providers. Matching C# and Java contracts now model transactional writes, immutable persisted envelopes, inbox acquisition, shared-storage leasing, transport dispatch, retry scheduling, and lost-lease outcomes. It does not make the current preview runtime transactional, and an in-memory implementation cannot satisfy the enterprise release gate.

The first PostgreSQL implementation now provides corresponding C# and Java persistence behavior against real transactional storage. Provider APIs are idiomatic to each ecosystem, while their observable state transitions conform to this document. Production promotion still requires the complete [Delivery Failure Matrix](../development/delivery-failure-matrix.md).

The PostgreSQL packages include a versioned schema, transaction-enlisted outbox writer, opt-in scoped Bus Outbox capture, shared-storage lease store, transaction-enlisted inbox store, supported delivery composition, and service-partitioned health/backlog inspection. The portable core includes transport dispatch that preserves the persisted body and identities, plus configurable .NET and Java background delivery lifecycles. There is not yet a cleanup service, transparent Consumer Outbox middleware, application-framework unit-of-work adapter, monitoring exporter integration, or automatic schema installation inside delivery composition. The provider's Testcontainers suites prove database atomicity, scoped send/publish capture, stable-envelope rehydration, competing leases, duplicate-completed acquisition, and deterministic O01/O02 persistence-boundary recovery. Live RabbitMQ gates and the Aspire showcase prove persisted-envelope consumption from C# to Java and Java to C#. These tests do not by themselves satisfy the complete O01–O06 production matrix.

The Transactional Outbox MVP is implemented as the first coherent Bus Outbox evaluation path: provider/host composition, explicit startup validation, focused recovery evidence, minimal dispatcher health and lag signals, and an end-to-end C#/Java Aspire showcase. This does not promote the provider for production. Transparent Consumer Outbox middleware, retention automation, additional database providers, durable scheduling, and the remaining process-level O01–O06 evidence are subsequent promotion work.

## Portable Runtime Surface

The corresponding runtime concepts are:

| Concept | C# | Java |
| --- | --- | --- |
| Transaction-enlisted write | `IOutboxWriter` | `OutboxWriter` |
| Scoped Bus Outbox transaction | `OutboxSession` | `OutboxSession` |
| Immutable persisted intent | `OutboxMessage` | `OutboxMessage` |
| Atomic lease storage | `IOutboxStore` | `OutboxStore` |
| Broker dispatch boundary | `IOutboxTransportDispatcher` | `OutboxTransportDispatcher` |
| Deterministic batch algorithm | `OutboxDispatcher` | `OutboxDispatcher` |
| Background delivery lifecycle | `OutboxDeliveryService` | `OutboxDeliveryService` |
| Deduplication key | `InboxMessageKey` | `InboxMessageKey` |
| Acquisition transaction | `IInboxTransaction` | `InboxTransaction` |
| Acquisition storage | `IInboxStore` | `InboxStore` |

The portable dispatcher preserves the stored message identity, conditionally marks only the current owner's lease as dispatched, reschedules failed attempts with a persisted due time, reports a lease lost after broker acceptance, and leaves a cancelled lease to expire for recovery. These are algorithm-level guarantees. Broker/database crash-window claims remain open until a supported provider passes O01–O06.

## Goals

- preserve outgoing send, publish, response, and fault intent in the same database transaction as application state
- prevent a redelivered message from applying the same protected application effect twice
- retain one stable message identity across persistence, dispatch retries, broker ambiguity, and redelivery
- support multiple application replicas without relying on process-local locks
- make schema compatibility, cleanup, and operational state observable

The model provides effectively-once protected application effects when the application uses the provider transaction correctly. It does not promise exactly-once broker delivery, global exactly-once execution, or distributed transactions with the broker.

## Required Message Identity

Inbox deduplication keys use the tuple `(consumer scope, message identity)`. The consumer scope is a stable logical endpoint or explicitly configured idempotency scope; it is not a host name, process id, or replica id.

The inbound message identity must be non-empty and stable across broker redelivery. A transport or serialization profile that cannot supply such an identity cannot enable the production inbox. Applications may supply an explicit idempotency key only through a documented provider policy; silently generating a new identity on receive is invalid because every redelivery would appear unique.

Every outbox message receives its final message identity before its record is committed. The dispatcher must reuse that identity for every attempt. It must never create a new identity because a previous broker outcome was ambiguous.

## Outbox Transaction Boundary

An outbox is owned by one logical producing service. Every persisted record carries a non-empty service partition. Replicas share its records and compete through persisted leases inside that partition. Other services may use partitions in the same database without reading or dispatching those records. A centralized dispatcher may deliberately own several configured partitions, but it must preserve their ownership and failure boundaries.

The application transaction atomically commits:

1. the application state change; and
2. every resulting outbox message record, including destination or publish intent, message identity, contract identity, serialized body, content type, headers, correlation metadata, creation time, and dispatch state.

The broker is not part of this transaction. No message may become eligible for dispatch before the application transaction commits. Rolling back the transaction removes both the application effect and its outgoing intent.

The provider must use the application's actual database transaction or a provider-owned unit of work that encloses the application callback. Writing an outbox record in a separate transaction is not a transactional outbox.

## Outbox Dispatch State Machine

Portable states are:

- `Pending`: committed and eligible for a dispatcher lease.
- `Leased`: owned temporarily by one dispatcher instance until a persisted deadline.
- `Dispatched`: the broker operation completed successfully according to the selected transport's acceptance contract.
- `Dead`: dispatch exceeded an explicit operator policy and requires intervention. This state is optional; providers must not silently discard records when it is absent.

A provider atomically leases due records. The lease records an owner and expiry time in shared storage. Another replica may reclaim an expired lease. Process-local queues and mutexes are insufficient.

After broker acceptance, the dispatcher marks the record `Dispatched`. If the process exits or storage fails between those steps, the lease eventually expires and the same record is dispatched again with the same message identity. A duplicate is therefore possible and detectable; this is the unavoidable broker/database commit gap.

A failed or ambiguous broker operation leaves the record retryable. Retry scheduling and attempt metadata are persisted. Cancellation or shutdown stops the current dispatcher wait but does not delete the record.

## Inbox Transaction Boundary

Before invoking protected application code, the provider atomically attempts to acquire `(consumer scope, message identity)` in the same database that owns the protected application effect. The acquisition result is one of:

- `Acquired`: this transaction may execute the application callback.
- `Completed`: the protected effect already committed; the runtime skips the callback and may settle the broker delivery successfully.
- `InProgress`: another live transaction or lease owns the identity; the runtime must not execute the callback and must leave the broker message eligible for retry.

For an acquired identity, the application transaction atomically commits:

1. the protected application state change;
2. any outgoing messages or responses as outbox records; and
3. the inbox record as `Completed`.

If the callback or transaction fails, none of those changes commit. The broker source is not settled as application success. A later delivery may acquire the identity and retry.

The database uniqueness constraint on `(consumer scope, message identity)` is the final concurrency authority. An in-memory pre-check may reduce contention but cannot determine correctness.

## Broker Settlement

The source message is settled successfully only after the inbox/application transaction commits. If the process exits after that commit but before broker settlement, redelivery observes `Completed`, skips the protected effect, and safely settles the source.

Outbox dispatch normally occurs independently after commit. Implementations may offer a best-effort wake-up signal after commit, but message correctness cannot depend on that signal; a polling or equivalent recovery path must find every committed `Pending` record.

## Responses and Faults

A response or fault produced inside an inbox-protected callback is outgoing message intent and must be persisted in the same outbox transaction. The request's correlation and response-address metadata must survive persistence unchanged.

When a duplicate request reaches an already completed inbox record, the application effect is not repeated. Providers that claim duplicate-request response recovery must persist enough response intent to confirm or replay the correlated response with the original identities. Without that capability, documentation must state that the duplicate is safely consumed but a caller may already have timed out.

## Cleanup and Retention

Cleanup is a persisted, bounded operation. It may remove only:

- `Dispatched` outbox records older than the configured audit and recovery retention; and
- `Completed` inbox records older than the maximum supported broker redelivery or replay window plus the configured safety margin.

Cleanup must not remove `Pending`, actively `Leased`, or retryable ambiguous records. It must not race an active inbox transaction. Providers must document the consequence of lowering retention and expose counts and oldest-record age before deletion.

## Schema and Rolling Upgrades

Every provider stores a schema version and validates it before consumers or dispatchers start. Migrations must be explicit and repeatable. Supported adjacent application versions must either share a read/write-compatible schema or startup must fail before processing; partial startup with incompatible readers and writers is forbidden.

Additive columns should have safe defaults for rolling deployments. Destructive changes require a staged migration across supported versions. Lease time, retry time, and cleanup comparisons use a documented UTC database-time or clock-skew policy.

## Provider Capability Contract

A production provider must declare and validate:

- supported database and minimum version
- transaction integration model
- atomic uniqueness and lease mechanism
- schema version and migration policy
- maximum batch size and lease duration
- retry and terminal-record policy
- inbox and outbox retention
- whether correlated response replay is supported
- health and metrics surface

The runtime must fail startup when inbox/outbox is enabled with missing transaction integration, an incompatible schema, an unsupported identity profile, or a provider that cannot supply atomic persistence.

## Observability

Providers expose bounded-cardinality metrics per logical service partition for pending, leased, dispatched, cancelled, dead, duplicate-completed, duplicate-in-progress, dispatch attempts, and failures. They also expose oldest eligible age, lease acquisition and expiry, broker dispatch duration, drain rate, active dispatcher replicas, last successful cycle, and dispatcher/inbox health without placing message bodies or arbitrary headers in telemetry. These requirements apply equally to embedded delivery services and standalone dispatcher fleets.

Logs and traces include provider name, logical consumer scope, operation, attempt, and outcome. Message identity may be logged only under the project's documented data-handling policy. Connection strings, credentials, serialized bodies, and application transaction contents are never emitted.

## Conformance and Promotion

The first provider is not production-promoted until both reference clients pass O01–O06 in the delivery failure matrix against real transactional storage. Tests must use at least two dispatcher or consumer replicas where concurrency is relevant and must inject failure:

- after the application commit but before dispatch
- after broker acceptance but before marking `Dispatched`
- after inbox commit but before source settlement
- while two replicas acquire one identity
- during an adjacent-version schema rollout
- while cleanup overlaps leasing and duplicate detection

The tests record application effects, broker messages, stable identities, persisted states, lease ownership, and settlement outcomes. Unit tests and in-memory stores may verify algorithms but are not release evidence.

## Deliberate Non-Goals for the First Provider

- distributed transactions or two-phase commit with a broker
- global ordering across outbox records
- cross-database atomicity
- automatic idempotency for side effects outside the provider transaction
- indefinite deduplication retention
- a generic workflow or saga engine
