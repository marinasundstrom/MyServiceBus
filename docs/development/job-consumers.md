# Job consumers

Status: preview design for the tracked job-execution MVP.

## Purpose

A job consumer handles application work that may outlive a normal broker delivery lock and therefore needs an independently persisted lifecycle. It is an application-level concept, not a synonym for every background loop or every scheduled message.

The public API should be familiar to MassTransit users while remaining portable between C# and Java. Compatibility means matching the core concepts and behavior where practical. It does not mean exposing MassTransit's saga messages, temporary endpoints, or job-service implementation as MyServiceBus contracts.

Use an ordinary consumer when acknowledging the broker delivery only after the handler returns is acceptable. Use a job consumer when the broker delivery should submit tracked work whose execution, retry, cancellation, and completion continue independently.

## Minimal public model

The first API surface contains:

- `IJobConsumer<TJob>` / `JobConsumer<TJob>` with a single `Run` / `run` method;
- `JobContext<TJob>` carrying the job, job and attempt identities, retry number, elapsed time, cancellation, and numeric progress reporting;
- `IJobClient` / `JobClient` for immediate submission, scheduled submission, cancellation, and manual retry;
- job-consumer registration with a timeout, per-instance concurrency limit, retry policy, and optional logical name;
- provider-neutral job, attempt, control-result, and monitoring state;
- an in-memory provider for development and a durable PostgreSQL provider for production-oriented evaluation.

The first lifecycle exposed to applications and monitoring is:

`Submitted -> Scheduled | Waiting -> Running -> Completed | Faulted | Cancelled`

Attempts are separate records. Retrying a job does not replace its job identity and each attempt receives a new attempt identity.

The MVP does not include arbitrary persisted consumer state, result values, batches, global concurrency, worker affinity, custom distribution strategies, finalization commands, or transport-specific job controls. These can be added without changing the basic consumer and client contracts.

## Execution semantics

Submitting a job persists or records its intent before it is eligible to run. A worker acquires an execution attempt without holding the original broker message lock. The runtime owns the authoritative transition to `Running`, `Completed`, `Faulted`, or `Cancelled`.

Cancellation is cooperative while the consumer is running. The runtime signals `JobContext.CancellationToken`; a consumer should observe that token and stop at a safe boundary. A timeout uses the same mechanism. A provider may eventually need a separate cancellation grace period, but that is not an application-facing MVP option.

An exception faults the attempt. The configured retry policy determines whether the same job returns to `Waiting` with another attempt or becomes `Faulted`. A normal return completes the job. Publishing or dispatching the command is never completion evidence.

Progress is an optional current value and optional limit. It is stored against the job, survives attempts, and is exported through monitoring. Progress updates may be buffered internally without changing the consumer contract.

## Registration and discovery

Explicit interface registration is the first supported path. C# and Java should normalize registration into the same conceptual descriptor:

- logical job type and message type;
- consumer implementation or invocation descriptor;
- endpoint/application identity;
- timeout;
- per-instance concurrency limit;
- retry policy.

Consumer-method declarations, attributes or annotations, C# source generation, and Java annotation processing follow later. They must produce the same descriptor and runtime pipeline. An attribute must not implicitly create a recurring definition; recurrence remains an explicit deployment-owned concern.

## Scheduling and recurrence

One-time message scheduling, recurring message publication, and tracked application jobs are distinct public concepts even when they share timers, leases, or PostgreSQL tables internally.

A scheduled tracked job creates a job record immediately with a future eligibility time. A recurring job definition creates a distinct occurrence, and that occurrence submits a distinct tracked job. The correlation is:

`definition -> occurrence -> job -> attempts`

The current preview recurring implementation dispatches an ordinary command and can authoritatively report only `Dispatched`. During the tracked-job slice it should be promoted so that registered application jobs enter the job executor and their occurrence can reach a terminal application outcome. If recurring message publication remains useful, it should receive a separately named facade rather than sharing `IRecurringJobScheduler` semantics.

Tracked recurring jobs default to forbidding overlapping executions. More permissive overlap policies remain explicit provider capabilities.

## Persistence and providers

The application-facing contracts do not expose storage-engine objects. The built-in durable implementation uses PostgreSQL for job intent, attempt history, leases, cancellation, progress, and the outbox boundary needed to dispatch work safely.

Until the first stable release, the PostgreSQL schema is a replaceable preview schema rather than a versioned compatibility contract. Development environments may be recreated as the job model evolves. Stable upgrade and migration guarantees begin with the first real release.

Hangfire and JobRunr are future ecosystem-specific adapters. They should project their lifecycle into the portable job and attempt states instead of adding their product-specific state machines to the core API. The built-in PostgreSQL provider remains the preferred cross-language profile.

## Monitoring boundary

The monitoring service receives authoritative job and attempt state from configured job sources. The dashboard consumes only the monitoring service and presents jobs under their owning application or service. Provider names, leases, worker identities, and raw attempt faults belong in drill-down views rather than the landing-page overview.

The monitoring data must distinguish:

- no jobs in the selected period;
- a source that has never reported;
- stale or interrupted reporting;
- an authoritative source that cannot currently be queried.

## MVP evidence

The feature is not complete until both runtimes demonstrate:

1. immediate, delayed, and recurring job submission;
2. completion, fault, automatic retry, manual retry, timeout, and cancellation;
3. per-instance concurrency enforcement;
4. restart recovery using PostgreSQL;
5. job and attempt monitoring correlated with the application and recurring occurrence;
6. matching contract and lifecycle tests in C# and Java;
7. the full Aspire sample showing the lifecycle in the dashboard.

## References

- [MassTransit job consumers](https://masstransit.io/documentation/concepts/job-consumers)
- [MassTransit job consumer configuration](https://masstransit.io/documentation/configuration/job-consumer)
- [Recurring jobs](recurring-jobs.md)
- [Scheduler provider architecture](scheduler-provider-architecture.md)
