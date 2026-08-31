# Recurring Jobs

## Purpose

Recurring jobs are the next scheduling theme. They exercise durable identity, repeated execution, provider ownership, and monitoring more deeply than another one-time scheduling adapter would. Quartz, Hangfire, and JobRunr remain later conformance providers; the first implementation should prove the model with the in-memory development runtime and the built-in durable provider using its PostgreSQL storage profile.

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

## Execution boundary

The built-in durable provider creates a tracked job for every occurrence. It can therefore report `Running`, `RetryScheduled`, `Completed`, `Cancelled`, and `Failed` from authoritative job execution state rather than treating broker acceptance as application completion. Job attempts preserve the occurrence identity across execution retries.

The volatile in-memory recurring provider remains a development baseline with a dispatch-only boundary. It may report creation and dispatch, but it cannot call an occurrence completed merely because a consumer command was accepted. Aligning that provider with in-memory tracked jobs is a remaining MVP slice.

The minimal tracked execution contract and its promotion path for recurring occurrences are specified in [Job Consumers](job-consumers.md). While the API is still in preview, `IRecurringJobScheduler` should become the facade for recurring tracked application jobs. If recurring publication to ordinary consumers remains desirable, it should be introduced separately as an `IRecurringMessageScheduler` instead of making one facade report two different meanings of completion.

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

Normal timer jitter is not a misfire. A due occurrence remains an ordinary occurrence until at least one later cadence instant has also passed. Once that boundary is crossed, `Skip` advances without dispatch, `FireOnceNow` emits one coalesced dispatch, and `CatchUp` emits the oldest due occurrences up to its configured cap. Any excess is skipped and the definition advances to the first future cadence instant; the cap cannot be bypassed through an immediate materialization loop. C# and Java run these rules against the same fixtures.

Overlap is independent from misfire handling:

- `Allow`: materialize each due occurrence regardless of earlier execution state;
- `Forbid`: do not dispatch a new occurrence while an earlier tracked execution is active;
- `Queue`: materialize the occurrence but hold dispatch until the previous one reaches a terminal state.

The current preview implements `Allow`. The durable provider now observes tracked completion, but `Forbid` and `Queue` still require explicit materializer policy and concurrency tests before they can be advertised. A provider must reject unsupported overlap requirements at registration or definition acceptance.

Occurrence execution retry is not recurrence. Retrying a failed occurrence preserves its occurrence identity and creates another attempt under the same tracked job. The definition continues according to its own cadence unless an explicit policy pauses it after failures. The durable provider projects automatic and manual retry, cancellation, completion, and final failure back to the occurrence.

## Built-in durable provider

The provider is a MyServiceBus facility; PostgreSQL is its first durable persistence and coordination substrate, not its product-level identity. This distinction leaves room for other storage profiles without changing the application facade or normalized monitoring model.

The PostgreSQL storage profile uses schema version 4 and tables separate from `outbox_message`:

- `recurring_job_definition` stores identity, revision, cadence, window, policy, safe job type, serialized command reference, current status, next due time, and audit timestamps;
- `recurring_job_occurrence` stores occurrence identity, definition revision, scheduled time, materialization reason, lifecycle, and the resulting tracked job identity;
- a unique key on definition identity prevents duplicate ownership;
- a unique key on definition id, revision, and scheduled time prevents duplicate scheduled occurrences.

A materializer leases due definitions with database time and `SKIP LOCKED`. In one transaction it creates the occurrence, writes a waiting tracked job with the final command envelope and registered execution policy, links both records, and advances the definition's next due time. A crash can repeat the transaction, but the uniqueness constraints prevent a second logical occurrence. Integration gates verify restart recovery, competing materializers creating one logical occurrence, Java materializing a C# definition, and C# materializing a Java definition against the same PostgreSQL database.

The schema stores the cadence contract and final envelope, not a language-specific scheduler object. PostgreSQL is therefore the promoted storage-interoperable profile of the built-in provider. The in-memory implementation uses the same state machine for development but reports volatile durability and loses definitions on restart.

The built-in durable provider reports the stable provider identity `MyServiceBus.Durable`; PostgreSQL appears as its storage profile in configuration and diagnostics. Definition registration stores an envelope template plus portable cadence metadata. Repeating the same semantic definition is idempotent even though transient envelope fields such as message id and sent time differ during registration. Materialization must replace those transient fields for every occurrence so inbox deduplication never mistakes later occurrences for duplicates.

The PostgreSQL profile accepts cadence instants and intervals at microsecond precision, matching PostgreSQL timestamp storage in both runtimes. A provider rejects finer values rather than rounding C# and Java definitions differently.

Due definitions are selected with row locks and `SKIP LOCKED`. Occurrence uniqueness, creation of a fresh envelope and job identity, tracked-job insertion, occurrence linkage, and advancement of the definition happen in one transaction. The occurrence begins `Pending`, becomes `Running` when a worker leases its job, and follows the job into retry or a terminal state. The .NET registration hosts the polling lifecycle automatically. Java exposes the equivalent `PostgreSqlRecurringJobService` with explicit `start()` and `close()` lifecycle.

## Provider profiles

MyServiceBus supports three deliberate deployment profiles rather than pretending that one scheduler engine is best for every application:

- **.NET-native:** every participating application is .NET. The built-in providers remain available, while a Hangfire adapter can use an established .NET scheduler when its operational maturity or ecosystem integration is preferred.
- **Java-native:** every participating application is Java. The same portable MyServiceBus contract can be backed by a Java scheduler such as JobRunr.
- **Mixed C# and Java:** applications share the built-in durable provider with PostgreSQL storage and its language-neutral schema, cadence contract, envelope format, and leasing rules.

The first two profiles promise API and monitoring-model consistency, not shared scheduler storage. A Hangfire database is not a cross-language contract for JobRunr, nor vice versa. Only the built-in provider's PostgreSQL storage profile promises that either MyServiceBus runtime can create and materialize the same definitions.

Applications depend on the recurring-job scheduler facade. Provider integrations implement the separate provider boundary and report their identity, durability, and placement. The built-in in-memory provider is the development baseline: it is process-local, volatile, and intentionally implements only capabilities it can guarantee. Provider-specific capabilities may be exposed in configuration and drill-down diagnostics, while the normalized definition and occurrence model remains stable for monitoring and the dashboard.

## Monitoring

The monitoring service is the store and query boundary; the dashboard does not query the scheduler database. The first monitoring slice exports bounded, command-body-free definition snapshots from each application in C# and Java. The snapshots are stored by the configured monitoring history provider and appear in separate system and application Recurring Jobs views.

A definition projection includes application, human-readable id and description, provider, durability, status, cadence summary, time zone, next occurrence, last materialization, revision, and snapshot freshness. A later occurrence projection will include its stable identity, definition identity and revision, scheduled time, actual materialization/dispatch times, reason, normalized and provider-native status, attempt metadata when available, and a safe failure category. Until that projection exists, the UI does not infer occurrence completion from definition state; materialized commands appear only in ordinary throughput and outbox evidence.

Coverage is explicit: authoritative current definitions, occurrence-history retention start, last successful query, snapshot time, and gaps. If the scheduler reports dispatch only, the dashboard says `Dispatched`; it does not show `Completed`. Missing data for a period is displayed as unknown coverage rather than zero executions.

The focused Recurring Jobs view currently shows definitions, cadence, next occurrence, provider profile, revision, snapshot freshness, and reporting-instance health. Occurrence history and actionable overview counts such as overdue or dispatch-failed remain follow-up slices that require stronger execution evidence.

## First implementation slices

1. Add shared definition, cadence, receipt, control-result, and occurrence contracts in C# and Java, with conformance fixtures but no provider persistence.
2. Add a volatile in-memory definition store and deterministic materializer tests for add-or-update, revisions, pause/resume/remove, trigger-now, and occurrence uniqueness.
3. Add PostgreSQL schema version 4, transactional occurrence materialization, restart tests, and bidirectional C#/Java storage interoperability.
4. Export definition monitoring with explicit snapshot freshness, then add a focused dashboard view and Aspire demo case. Add retained occurrence monitoring only when dispatch lifecycle evidence is available.
5. Add cron evaluation only after cross-language fixtures cover dialect, time zones, daylight-saving transitions, boundaries, and misfires. Fixed interval may ship first if cron would otherwise obscure the state model.
6. Implement the job-execution/`JobConsumer` layer described in [Job Consumers](job-consumers.md), beginning with interface handlers and explicit registration, and promote durable recurring occurrences into tracked jobs. Method handlers and generated C#/Java registration remain later conveniences.
7. Validate the provider boundary with a .NET Hangfire conformance adapter and a Java JobRunr conformance adapter without making either engine mandatory or claiming storage interoperability between them.

## References

- [MassTransit message scheduler configuration](https://masstransit.io/documentation/configuration/scheduling)
- [MassTransit job consumers and recurring jobs](https://masstransit.io/documentation/concepts/job-consumers)
- [Hangfire recurring jobs](https://docs.hangfire.io/en/latest/background-methods/performing-recurrent-tasks.html)
- [JobRunr recurring jobs](https://www.jobrunr.io/en/documentation/background-methods/recurring-jobs/)
