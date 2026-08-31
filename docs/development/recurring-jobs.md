# Recurring Jobs

## Purpose

Recurring jobs are the next scheduling theme. They exercise durable identity, repeated execution, provider ownership, and monitoring more deeply than another one-time scheduling adapter would. Quartz, Hangfire, and JobRunr remain later conformance providers; the first implementation should prove the model with the in-memory development runtime and the shared MyServiceBus PostgreSQL provider.

The public model should be familiar to MassTransit users without copying its saga implementation. MassTransit usefully distinguishes message scheduling from job consumers and offers add-or-update recurring jobs. MyServiceBus keeps those ideas, but makes definition state, occurrence state, provider capability, and monitoring coverage explicit.

## Meaning of a recurring job

A recurring job is a durable **definition** that creates uniquely identified **occurrences** according to a cadence. It is not one scheduled-message record that changes its due time repeatedly.

```text
Definition: daily-invoice-export, revision 4, active
  occurrence A: scheduled for 2026-09-01T01:00Z, dispatched
  occurrence B: scheduled for 2026-09-02T01:00Z, pending
```

Definitions and occurrences have different lifecycles:

- a definition can be active, paused, ended, or removed;
- an occurrence can be pending, acquired, dispatched, running, retrying, completed, cancelled, skipped, or failed;
- changing a definition creates a new revision and does not rewrite an occurrence already materialized from an earlier revision;
- pausing a definition stops new scheduled occurrences but does not imply that already dispatched work can be recalled;
- triggering a definition now creates a manual occurrence without moving its next scheduled time.

The outbox polling loop is not a recurring job. It is runtime infrastructure even if it uses similar timer and lease mechanics internally.

## Honest first execution boundary

The first recurring slice dispatches a job command through MyServiceBus to an ordinary application consumer. The scheduler can authoritatively report that an occurrence was created and dispatched. It cannot call that occurrence completed merely because the broker accepted the message.

End-to-end `Running`, `Completed`, progress, cooperative cancellation, persisted job state, long-running lock avoidance, and execution retry belong to a later `JobConsumer`-style layer. That layer can correlate its execution records with the existing definition and occurrence identities. This keeps the first recurring feature useful without pretending that message delivery and application work completion are the same event.

Recurring messages remain a separate facade. They may reuse the same cadence and materialization machinery, but their outcome is message delivery rather than a tracked application job. The dashboard must label each kind accurately.

## Job definition and discovery

How application code implements a job is separate from how a recurring definition selects its cadence and scheduler provider. The future job-execution layer should support the same range of discovery styles as consumers:

- standard job interfaces for explicit, conventional handlers;
- method-based jobs for applications that prefer consumer-method-style registration;
- manual registration for dynamic or framework-integrated scenarios;
- generated registration using a C# source generator and a Java annotation processor.

Annotations or attributes may describe execution concerns such as the logical job name, queue or endpoint, concurrency, timeout, retry, and cancellation support. Generated and reflective discovery must produce the same normalized job descriptor and invoke the same runtime pipeline. An attribute must not silently create a recurring schedule: recurrence remains an explicit deployment-owned definition with its own identity and revision.

Generated registration is the preferred production path when definitions are static because it improves startup behavior and native/AOT compatibility. Reflection remains a supported convenience and fallback. C# and Java metadata should express equivalent portable behavior, while language-specific extensions stay outside the shared contract.

## Application API

The C# and Java APIs should expose equivalent concepts using idiomatic asynchronous types:

```text
RecurringJobScheduler
  addOrUpdate(definition, job command) -> definition receipt
  pause(identity, expected revision?) -> control result
  resume(identity, expected revision?) -> control result
  remove(identity, expected revision?) -> control result
  triggerNow(identity) -> occurrence receipt
  get(identity) -> definition snapshot
```

The identity consists of an owner application, optional group, and caller-supplied schedule id. A returned receipt includes the stable definition id, current revision, provider identity, actual durability, accepted time, and next occurrence when known. Control results distinguish applied, unchanged, revision conflict, already running or dispatched where relevant, unsupported, and not found.

`addOrUpdate` is idempotent for the same identity and semantic content. A changed cadence, job command, scheduling window, or policy increments the revision. Callers may supply an expected revision to prevent one deployment from silently overwriting another deployment's schedule.

Job commands are ordinary serializable message contracts. The durable provider stores the final MyServiceBus envelope or an opaque payload reference, never a delegate, lambda, expression tree, Java method reference, or provider-native application type invocation.

## Cadence contract

Cron syntax is not universal. Hangfire commonly uses five-field cron expressions, Quartz uses a seconds-aware dialect, and provider time-zone support differs. A raw unqualified `CronExpression` string is therefore insufficient for a portable contract.

The cadence is a versioned union:

```text
FixedInterval
  interval
  anchor time

Cron
  expression
  dialect: Unix5 | Quartz
  IANA time-zone id
```

The PostgreSQL interoperability profile initially supports `FixedInterval` and one documented cron dialect only after identical C# and Java next-occurrence fixtures pass. Provider adapters advertise their supported cadence kinds and dialects. They may reject rather than reinterpret an expression. Provider-native extensions stay in provider configuration, outside the portable definition.

Every definition may also contain an inclusive start time and exclusive end time. All instants are stored in UTC; the IANA time-zone id is retained for future calendar evaluation and display. Daylight-saving gaps and overlaps are covered by shared fixtures before cron is promoted beyond preview.

## Misfires, overlap, and failure

A **misfire** is a scheduled time that passed while the definition could not be materialized. Portable policies are deliberately small:

- `Skip`: advance to the first future time;
- `FireOnceNow`: create one coalesced occurrence, then continue in the future;
- `CatchUp`: create missed occurrences up to a required configured cap.

`FireOnceNow` is the proposed default because it preserves the intent to run without producing an unbounded burst after downtime. The occurrence records the original missed window and that it was coalesced.

Overlap is independent from misfire handling:

- `Allow`: materialize each due occurrence regardless of earlier execution state;
- `Forbid`: do not dispatch a new occurrence while an earlier tracked execution is active;
- `Queue`: materialize the occurrence but hold dispatch until the previous one reaches a terminal state.

The first dispatch-only slice can implement `Allow`. It cannot honestly implement `Forbid` or `Queue` across application execution because it does not yet observe completion. Those policies require the job-execution layer. A provider must reject unsupported overlap requirements at registration or definition acceptance.

Occurrence execution retry is not recurrence. Retrying a failed occurrence preserves its occurrence identity and increments an attempt identity. The definition continues according to its own cadence unless an explicit policy pauses it after failures. The first dispatch-only slice relies on ordinary message delivery guarantees and reports dispatch failure without inventing application retry state.

## PostgreSQL ownership

PostgreSQL uses tables separate from `outbox_message`:

- `recurring_job_definition` stores identity, revision, cadence, window, policy, safe job type, serialized command reference, current status, next due time, and audit timestamps;
- `recurring_job_occurrence` stores occurrence identity, definition revision, scheduled time, materialization reason, lifecycle, and the resulting outbox record identity;
- a unique key on definition identity prevents duplicate ownership;
- a unique key on definition id, revision, and scheduled time prevents duplicate scheduled occurrences.

A materializer leases due definitions with database time and `SKIP LOCKED`. In one transaction it creates the occurrence, writes the final command envelope to the existing outbox, and advances the definition's next due time. A crash can repeat the transaction, but the uniqueness constraints prevent a second logical occurrence. C# and Java use the same schema and may materialize definitions created by either client.

The schema stores the cadence contract and final envelope, not a language-specific scheduler object. PostgreSQL is therefore the promoted storage-interoperable provider. The in-memory implementation uses the same state machine for development but reports volatile durability and loses definitions on restart.

## Provider profiles

MyServiceBus supports three deliberate deployment profiles rather than pretending that one scheduler engine is best for every application:

- **.NET-native:** every participating application is .NET. The built-in providers remain available, while a Hangfire adapter can use an established .NET scheduler when its operational maturity or ecosystem integration is preferred.
- **Java-native:** every participating application is Java. The same portable MyServiceBus contract can be backed by a Java scheduler such as JobRunr.
- **Mixed C# and Java:** applications share the MyServiceBus PostgreSQL provider and its language-neutral schema, cadence contract, envelope format, and leasing rules.

The first two profiles promise API and monitoring-model consistency, not shared scheduler storage. A Hangfire database is not a cross-language contract for JobRunr, nor vice versa. Only the PostgreSQL profile promises that either MyServiceBus runtime can create and materialize the same definitions.

Applications depend on the recurring-job scheduler facade. Provider integrations implement the separate provider boundary and report their identity, durability, and placement. The built-in in-memory provider is the development baseline: it is process-local, volatile, and intentionally implements only capabilities it can guarantee. Provider-specific capabilities may be exposed in configuration and drill-down diagnostics, while the normalized definition and occurrence model remains stable for monitoring and the dashboard.

## Monitoring

The monitoring service receives separate normalized records for definitions and occurrences. It remains the store and query boundary; the dashboard does not query the scheduler database.

A definition projection includes application, human-readable id and description, provider, durability, status, cadence summary, time zone, next occurrence, last materialization, revision, and snapshot freshness. An occurrence includes its stable identity, definition identity and revision, scheduled time, actual materialization/dispatch times, reason, normalized and provider-native status, attempt metadata when available, and a safe failure category.

Coverage is explicit: authoritative current definitions, occurrence-history retention start, last successful query, snapshot time, and gaps. If the scheduler reports dispatch only, the dashboard says `Dispatched`; it does not show `Completed`. Missing data for a period is displayed as unknown coverage rather than zero executions.

The application overview shows only actionable counts such as active, paused, overdue, dispatch-failed, and next due. A focused Recurring Jobs view shows definitions and occurrence history. Provider details remain a drill-down.

## First implementation slices

1. Add shared definition, cadence, receipt, control-result, and occurrence contracts in C# and Java, with conformance fixtures but no provider persistence.
2. Add a volatile in-memory definition store and deterministic materializer tests for add-or-update, revisions, pause/resume/remove, trigger-now, and occurrence uniqueness.
3. Add PostgreSQL schema version 4, transactional materialization into the existing outbox, restart tests, and bidirectional C#/Java storage interoperability.
4. Export definition and occurrence monitoring with explicit dispatch-only coverage, then add a focused dashboard view and Aspire demo case.
5. Add cron evaluation only after cross-language fixtures cover dialect, time zones, daylight-saving transitions, boundaries, and misfires. Fixed interval may ship first if cron would otherwise obscure the state model.
6. Design the job-execution/`JobConsumer` layer for completion, progress, retry, concurrency, cancellation, interface and method handlers, and generated C#/Java registration.
7. Validate the provider boundary with a .NET Hangfire conformance adapter and a Java JobRunr conformance adapter without making either engine mandatory or claiming storage interoperability between them.

## References

- [MassTransit message scheduler configuration](https://masstransit.io/documentation/configuration/scheduling)
- [MassTransit job consumers and recurring jobs](https://masstransit.io/documentation/concepts/job-consumers)
- [Hangfire recurring jobs](https://docs.hangfire.io/en/latest/background-methods/performing-recurrent-tasks.html)
- [JobRunr recurring jobs](https://www.jobrunr.io/en/documentation/background-methods/recurring-jobs/)
