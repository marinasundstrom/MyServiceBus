# Scheduler Provider Architecture

## Goal

MyServiceBus needs one scheduling architecture that can use different engines without allowing Quartz, Hangfire, JobRunr, a broker, or an outbox schema to define the public model accidentally. The architecture must support application-oriented monitoring as a first-class requirement, not as a dashboard screen inferred from provider tables later.

The goal is behavioral parity, not a universal scheduler product. A .NET application may use Hangfire while a Java application uses JobRunr. They do not need to share the provider database or execute one another's native jobs. They do need to expose the same MyServiceBus concepts, identities, lifecycle states, capabilities, and monitoring records. When C# and Java applications must share scheduled message storage and execution directly, the MyServiceBus PostgreSQL scheduler is the promoted portable provider.

The public concepts remain separate:

- a **scheduled message** is one publish or send with one due time;
- a **one-time application job** is one tracked registered application operation, submitted now or with one due time;
- a **recurring message definition** describes a cadence that produces distinct message deliveries;
- a **recurring application-job definition** describes a cadence that produces distinct tracked job occurrences;
- an **execution** is one attempt lifecycle for one scheduled item or recurring occurrence;
- an **infrastructure loop**, such as the outbox dispatcher, is runtime machinery and is not a user job.

These concepts may share a cadence value, scheduling engine, and execution runner. They do not share one dashboard list or pretend to have identical lifecycle and control semantics. A recurring message occurrence is a message delivery; a recurring application-job occurrence may additionally have progress, concurrency, timeout, cancellation, and result semantics.

## Deployment shapes

The abstraction covers the major places in which scheduling can happen. It does not attempt to flatten every advanced option from every engine.

| Shape | Scheduling owner | Typical use | Durable | Shared across C# and Java | Recurring work |
| --- | --- | --- | --- | --- | --- |
| Process-local delay | Application process | Development, tests, short-lived convenience | No | No | Not a durable definition |
| Broker-native delay | Message transport | Efficient delayed delivery where the broker supports it | Broker-dependent | Potentially, through transport interop | Usually no |
| Embedded scheduler | Application process plus provider store | Application-owned delayed messages and jobs | Configuration-dependent | Usually not; engines may be ecosystem-specific | Provider-dependent |
| Remote scheduler service | Separate service reached through commands or an API | Central scheduling ownership and independent scaling | Service-dependent | Yes when it accepts portable MyServiceBus commands | Service-dependent |
| Transactional outbox scheduler | Application database and dispatcher | Atomic application state plus delayed message intent | Yes | Yes with the MyServiceBus PostgreSQL schema and envelope contract | Not initially |
| External job processor | Hangfire, JobRunr, or a similar worker system | Rich application jobs, retries, history, and recurrence | Yes with persistent storage | No native job sharing; normalized monitoring only | Yes |

Provider placement is explicit metadata: `ProcessLocal`, `BrokerNative`, `Embedded`, `RemoteService`, or `TransactionalOutbox`. A provider interface may be implemented by a local adapter or by a client for a separate scheduler service. Calling the same interface does not imply that execution happens in the caller.

Remote acceptance needs its own stable command identity and idempotency behavior. A client receipt reports the provider identity, placement, acceptance time, durability actually established, and provider-native handle when safe to expose. Sending a command toward a service is not yet durable acceptance unless that service has acknowledged ownership.

## Replace the callback seam

The current `IJobScheduler` / `JobScheduler` accepts an executable callback. It is useful as a local timer and deterministic test seam, but a callback cannot be persisted, inspected, migrated, or executed by another process safely. It must not become the Quartz or Hangfire integration contract.

Because the project is still in preview, the current interface should be renamed to an explicitly process-local abstraction such as `ILocalDelayScheduler` / `LocalDelayScheduler`. It may remain public for advanced composition, but its name and documentation must make volatility unavoidable.

A new provider-neutral scheduler SPI should accept serializable execution commands. This should evolve the existing `IScheduleMessageProvider` / `ScheduleMessageProvider` seam for messages rather than creating a second competing message-provider abstraction. The exact C# and Java APIs may be idiomatic, but they represent the same records and behavior:

```text
SchedulerProvider
  capabilities
  schedule(execution command) -> schedule receipt
  cancel(schedule identity) -> normalized result

RecurringScheduleProvider
  upsert(definition)
  pause / resume / remove / trigger-now

SchedulerStateSource
  provider health and freshness
  current definitions
  current executions
  optional retained history and coverage

SchedulerEventObserver
  definition and execution lifecycle events
```

`IMessageScheduler` / `MessageScheduler` remains the application-facing bus facade. It creates the final message envelope and submits a built-in message execution command. A future `IRecurringMessageScheduler` / `RecurringMessageScheduler` manages recurring message definitions using the familiar MassTransit separation. A future application `JobClient` submits immediate or scheduled registered job requests, while a `RecurringJobScheduler` manages recurring application-job definitions. These facades may share lower-level timing and execution infrastructure without exposing engine objects or collapsing their semantics.

The current callback interface already uses the `IJobScheduler` / `JobScheduler` name. Renaming it to `ILocalDelayScheduler` / `LocalDelayScheduler` must happen before introducing the application-job facade, so “job” cannot mean both a volatile callback timer and durable application work.

Execution location is separate from timing location. A due command may ask the provider to dispatch a message back to an application, or it may invoke a registered handler in a colocated worker. Distributed applications should prefer dispatching a portable occurrence message when work belongs to an application. Embedded Hangfire or JobRunr integrations may invoke the generic MyServiceBus runner locally, but provider-native method or lambda metadata remains private to the adapter.

## MassTransit compatibility

MassTransit provides a useful application-facing split:

- `IMessageScheduler` schedules one-time sends and publishes and returns a token that can be used for cancellation;
- `IRecurringMessageScheduler` keeps recurring message definitions and their pause, resume, and cancellation controls separate;
- transport-native delayed delivery and a scheduler endpoint are configured as different mechanisms.

MyServiceBus should preserve that familiarity for message scheduling where it improves transition and interoperation. Equivalent concepts should have equivalent behavior, and familiar names are preferable when they remain accurate. The interfaces do not need to be copies. MyServiceBus also needs application jobs, provider capabilities, placement, acceptance guarantees, state coverage, and normalized monitoring; those concerns are not expressed sufficiently by a destination URI and schedule token alone.

MassTransit's job consumers are a separate distributed job-service concept, including job submission, attempts, concurrency limits, cancellation, retry, and progress. They confirm that a scheduled message and an application job should not be collapsed into one public abstraction. MyServiceBus should consider a comparable convenience only after the underlying scheduled-message, job-execution, and monitoring primitives are stable. It does not need to reproduce MassTransit's saga-backed implementation.

The provider SPI therefore sits below the MassTransit-familiar application facade. It is a MyServiceBus integration contract, not an interop wire contract and not a promise that Hangfire and JobRunr can share jobs. The PostgreSQL provider is the recommended shared C#/Java route. Transport-native scheduling can be the preferred route when compatible applications already share a broker and its delayed-delivery semantics are sufficient.

## Portable execution command

Providers persist a versioned command, not an application delegate, lambda, expression tree, reflection method, or arbitrary object graph. The portable command contains:

- execution identity and optional recurring-definition/occurrence identity;
- semantic kind and stable handler key, such as `myservicebus.message.v1`;
- due time, creation time, owner application, and logical partition;
- payload schema version and media type;
- either bounded inline payload bytes or an opaque reference owned by a configured payload store;
- safe correlation and trace references;
- provider-neutral retry and misfire policy identifiers where supported.

For scheduled messages, the payload is the final serialized bus envelope plus destination and publish/send intent. Dispatch never reloads an application message type or assigns a new `MessageId`. For application jobs, the handler key resolves through an explicit registry in the executing application. Removing or changing a handler with pending work is a versioning event that must fail visibly.

Provider adapters invoke one generic MyServiceBus runner. A Hangfire adapter must not persist the user's method expression directly, and a JobRunr adapter must not persist a Java lambda directly. This keeps durable data independent of language type and method names even when the underlying product normally encourages those APIs.

## Capabilities are configuration data

Provider selection must expose a capability descriptor at startup:

- volatile or durable storage;
- one-time messages and/or one-time application jobs;
- recurring definitions;
- cancellation, pause/resume, remove, trigger-now, and retry controls;
- misfire policies and scheduling precision;
- automatic retry and maximum-attempt support;
- clustering and competing execution;
- current-state query, retained history, and lifecycle event support;
- inline payload limits and external payload references;
- transaction enlistment model;
- provider dashboard or management API availability.

Registration declares the required capabilities and fails before message processing when the selected engine cannot provide them. A Quartz RAM store is volatile even though the Quartz adapter can also use a persistent store. A provider cannot report `Durable` based only on its product name.

## Transaction boundary

Scheduling acceptance and application-state commit are separate unless a provider proves enlistment in the caller's transaction. Quartz and Hangfire normally use their own storage transaction; JobRunr's application transaction integration is a separate commercial capability. Provider-specific enlistment features do not become a portable guarantee automatically.

The PostgreSQL outbox scheduler remains the supported path when application state and delayed message intent must commit atomically. A future scheduler-outbox bridge could record a provider command in the application outbox and submit it after commit, but that has a different acceptance point and cancellation race and requires its own design.

## Normalized lifecycle

MyServiceBus owns a small common state vocabulary and retains the provider's native state alongside it:

```text
Pending -> Acquired/Running -> Completed
   |              |
   +-> Cancelled  +-> RetryScheduled -> Running
                  +-> Failed
```

Definitions and executions are different records. A recurring definition has identity, cadence, time zone, next occurrence, enabled/paused state, misfire policy, and revision. Each occurrence has a separate identity and execution history. Updating a definition must not rewrite the identity of an already-created occurrence.

Cancellation and execution race through provider-owned atomic state where available. Results distinguish cancelled, already cancelled, too late/running, unsupported, wrong kind, and not found. Providers with weaker native operations must not claim stronger results by guessing from absence.

## Monitoring contract

Monitoring is supplied by the provider integration to the monitoring exporter; the dashboard never connects directly to Quartz, Hangfire, JobRunr, or an application scheduler database.

The normalized monitoring model has separate streams and queries for:

1. scheduled messages;
2. recurring message definitions and delivery occurrences;
3. immediate and scheduled application jobs;
4. recurring application-job definitions, occurrences, and execution attempts;
5. scheduler-provider health, workers, queues, polling lag, and data freshness.

Each state source reports coverage explicitly:

- whether current state is authoritative;
- whether terminal history is available;
- the oldest retained history time when known;
- the snapshot time and last successful provider query;
- gaps or degraded access.

Lifecycle events provide low-latency updates and terminal outcomes. Authoritative snapshots reconcile current state after restart or missed events. The central monitoring service stores normalized history. When an engine, such as Quartz, does not retain completed one-shot history, the UI must say that earlier outcomes are unavailable rather than treating an absent job as success. Message bodies, arbitrary job arguments, connection details, and provider dashboard secrets are never exported.

Provider-native status, attempt count, next attempt, misfire count, last failure category, and safe error summary may be retained. Full stack traces and payload inspection require a separately secured debugging surface.

Control is a later contract over the same identities and capability descriptor. Cancel, retry, pause, resume, trigger-now, and delete require authentication, authorization, audit, confirmation where destructive, and explicit partial-failure results. Read-only monitoring does not imply those permissions.

## Technology comparison

| Concern | PostgreSQL outbox scheduler | Quartz.NET / Quartz Scheduler | Hangfire | JobRunr |
| --- | --- | --- | --- | --- |
| Ecosystems | C# and Java implementation | Native C# and Java projects | .NET | Java/JVM |
| License baseline | MyServiceBus MIT plus PostgreSQL driver terms | Apache-2.0 | LGPLv3 or commercial | LGPLv3 or commercial |
| Native unit | Persisted final message envelope | Job plus trigger and job data | Serialized .NET method invocation and state machine | Serialized Java job/lambda/request and state machine |
| One-time scheduling | Yes | Yes | Yes | Yes |
| Recurring definitions | No | Yes | Yes | Yes, with OSS scale/feature limits |
| Automatic execution retry/history | MyServiceBus outbox policy and rows | Must be designed with listeners/triggers/history | Built in | Built in |
| Persistent storage | PostgreSQL only | Optional RAM or multiple persistent stores | Storage provider required for durability | Storage provider required for durability |
| Atomic with application transaction | Yes, caller-owned PostgreSQL transaction | Not by default; platform-specific enlistment exists | Storage/application integration specific | Transaction plugin is commercial |
| Native dashboard | No | No first-party operational dashboard | Yes, includes controls and arguments | Yes; richer security/search/control is commercial |
| Portable integration fit | Message-specific reference implementation | Best common cross-language engine test | Valuable .NET job-processor adapter | Valuable Java job-processor adapter |

Quartz is the first external provider because comparable Quartz implementations exist in both ecosystems under a permissive license. This lets the same conformance scenarios exercise both adapters, but it does not require them to share a Quartz database or claim cross-language Quartz compatibility. Hangfire and JobRunr remain important ecosystem-specific comparison providers because their richer state machines expose history, retry, recurring, and operational requirements Quartz leaves to the integration. They should be implemented only through the portable command and monitoring contracts, not used as the source of those contracts.

## Conformance levels

The test suites distinguish three claims so that “supported in both languages” is not ambiguous:

1. **API parity** — C# and Java expose equivalent MyServiceBus concepts and documented outcomes.
2. **Behavioral conformance** — different adapters pass the same lifecycle, identity, cancellation, retry, recurrence, freshness, and failure scenarios independently.
3. **Storage interoperability** — C# and Java can create, observe, cancel, and execute the same persisted scheduled records. This is a stronger provider-specific claim, initially reserved for the MyServiceBus PostgreSQL provider.

Quartz.NET versus Quartz Scheduler, and Hangfire versus JobRunr, can satisfy the first two levels without satisfying the third. The dashboard consumes normalized monitoring records, so it can present one distributed application even when its services use different scheduling engines.

## Implementation slices

The architecture should be delivered in independently reviewable slices:

1. rename the callback timer to make process-local volatility explicit;
2. enrich the existing message-provider contract with placement, capabilities, acceptance, and monitoring coverage without adding recurring jobs;
3. complete PostgreSQL scheduled-message interoperability and conformance as the promoted C#/Java path;
4. add Quartz.NET and Quartz Scheduler adapters as independent behavioral-conformance providers;
5. add scheduled-message monitoring views, including explicit freshness and missing-history states;
6. define recurring message/job definitions and occurrences, then validate them against Quartz and at least one richer ecosystem-specific engine family;
7. consider a higher-level application job consumer/service after the execution and monitoring primitives have proved sufficient.

Provider controls and dashboard management remain later slices. A read-only scheduling overview must not acquire queue-reset, delete, retry, or trigger permissions accidentally.

## Completion gates

One-time scheduling remains preview until the PostgreSQL provider passes cross-language conformance and the Quartz.NET and Quartz Scheduler adapters independently pass the same behavioral suite for durable acceptance, restart recovery, stable identity, due-time/misfire behavior, cancellation races, dispatch ambiguity, monitoring freshness, and missing-history transparency. Cross-language conformance means shared MyServiceBus behavior; only the PostgreSQL provider is initially expected to share scheduled records between C# and Java.

Recurring jobs remain a separate future slice. Before that feature is considered complete, its definition and occurrence contracts must be exercised by at least two materially different engines. Quartz supplies the common cross-language engine; Hangfire and JobRunr should be used as comparative adapters or executable design spikes so their richer history and control models do not become impossible to represent.

## References

- [MassTransit message scheduler configuration](https://masstransit.io/documentation/configuration/scheduling)
- [MassTransit job consumers](https://masstransit.io/documentation/concepts/job-consumers)
- [Quartz.NET job stores](https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/job-stores.html)
- [Quartz Scheduler documentation](https://www.quartz-scheduler.org/documentation/)
- [Hangfire background methods and states](https://docs.hangfire.io/en/latest/background-methods/index.html)
- [Hangfire recurring jobs](https://docs.hangfire.io/en/latest/background-methods/performing-recurrent-tasks.html)
- [Hangfire licensing](https://www.hangfire.io/licenses.html)
- [JobRunr architecture](https://www.jobrunr.io/en/documentation/)
- [JobRunr recurring jobs](https://www.jobrunr.io/en/documentation/background-methods/recurring-jobs/)
