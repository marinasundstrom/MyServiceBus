# Quartz Message Scheduling Provider

This provider design is subordinate to the [scheduler provider architecture](scheduler-provider-architecture.md), which defines the shared execution, capability, monitoring, and recurring-job boundaries after comparison with Hangfire and JobRunr.

## Purpose

The Quartz provider is the second independent implementation of MyServiceBus message scheduling. It validates the provider boundary against Quartz.NET and Quartz Scheduler rather than treating the default timer and PostgreSQL outbox implementations as sufficient architectural evidence. The two adapters share MyServiceBus behavior and tests; they are not required to share a Quartz database or native persisted records.

This slice covers one-time scheduled publish and send, cancellation, restart recovery, monitoring state, and C#↔Java behavioral parity. Recurring jobs remain a separate public capability. The provider may later supply the timing engine for recurring work, but that must not merge the one-time message and recurring-job models.

## Boundaries

- The provider stores a versioned, string-only command containing the final serialized message envelope and bounded routing metadata. It never stores a delegate, closure, application message object, or language-specific type name as executable state.
- The schedule token is the message identity. Quartz job and trigger identities are derived deterministically from it within a MyServiceBus-owned group.
- A persistent Quartz job store can make the schedule durable across process restart. A RAM job store remains volatile. Registration must declare the expected durability, and startup must fail if the scheduler metadata contradicts that declaration.
- Quartz scheduling normally commits independently from application data. It is not a transactional outbox and must not imply atomic application-state-plus-schedule behavior. Applications needing that guarantee use the PostgreSQL outbox scheduler. Platform-specific transaction enlistment is not part of the portable provider contract.
- The job dispatches the stored envelope through the normal transport adapter while preserving message identity, destination, intent, content type, correlation metadata, and headers.
- A one-shot Quartz trigger disappearing does not by itself distinguish completion, cancellation, or unknown identity. The adapter therefore maintains bounded lifecycle observations and exports the currently persisted Quartz schedules as authoritative pending state. It must not invent terminal history after restart when no provider-owned history store exists.

## Execution semantics

1. Build the final envelope at schedule time and assign the schedule token as `MessageId`.
2. Store one durable Quartz job and one one-shot trigger with explicit identities.
3. Return only after Quartz accepts both definitions.
4. At the due time, reconstruct the persisted envelope and dispatch it without reserializing the application message.
5. On success, record `Completed` and remove the one-shot job.
6. On failure, record a bounded failure category and reschedule according to an explicit retry policy; never spin through an unbounded immediate-refire loop.
7. Cancellation succeeds only while the trigger can be removed before execution wins. A missing provider-owned identity is `NotFound`; a known running or terminal identity is `TooLate`; a repeated known cancellation is `AlreadyCancelled` while retained in bounded local history.

Misfires use a documented fire-now policy for one-time messages. This preserves eventual delivery after scheduler downtime while making the delayed execution visible through provider status and attempt metadata.

## Required evidence

- publish and directed-send preserve the final envelope and stable identity;
- a pending schedule survives a process restart with a persistent Quartz store;
- RAM-store registration reports volatile and persistent-store registration reports durable;
- due work is not dispatched early and a misfired schedule is dispatched after recovery;
- cancellation before acquisition prevents dispatch, repeated cancellation is idempotent, and the cancellation/execution race has one winner;
- dispatch failure retries with bounded delay and does not create a second logical schedule;
- C# and Java expose equivalent provider, status, monitoring, and configuration behavior;
- the provider does not claim application transaction atomicity or recurring-job support.

## Provider packaging

The adapters are optional artifacts: `Sundstrom.MyServiceBus.Quartz` for .NET and `myservicebus-quartz` for Java. Applications configure and own the Quartz scheduler and job store; MyServiceBus registers its message job, provider, dispatcher, and scheduled-work source against that scheduler.

Quartz.NET 3.20 and Quartz Scheduler 2.5 are the initial integration baselines. Both projects recommend explicit persistent identities and string/primitive job data for durable stores. Their official documentation remains authoritative for scheduler and database configuration:

- [Quartz.NET job stores](https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/job-stores.html)
- [Quartz.NET Microsoft DI integration](https://www.quartz-scheduler.net/documentation/quartz-3.x/packages/microsoft-di-integration.html)
- [Quartz Scheduler documentation](https://www.quartz-scheduler.org/documentation/)

## Why Quartz before Hangfire

Hangfire is a stronger all-in-one .NET background-processing product. Its persisted state machine, automatic retries, retained job history, recurring scheduler, and dashboard would reduce the amount of provider-specific operational code. Those strengths also make it a useful future adapter for applications that already run Hangfire.

It is not the contract-defining provider for MyServiceBus:

- Hangfire persists .NET type, method, parameter, and argument metadata. MyServiceBus needs a language-neutral final-envelope command instead of a serialized application method invocation.
- Hangfire has no Java implementation. JobRunr is inspired by Hangfire and offers a comparable Java product, but the two engines have different storage, retry, recurring, history, dashboard, and open-source/commercial feature boundaries. Pairing them would test two product-specific adapters rather than one cross-language provider model.
- Hangfire Core is LGPLv3 or commercially licensed, while Quartz.NET and Quartz Scheduler are Apache-2.0. Optional LGPL integration can remain separate, but it should not become a dependency of the permissively licensed portable foundation.
- Hangfire and JobRunr are application job processors as well as schedulers. Their method/lambda-first APIs are a better fit for the future recurring-job and background-job stories than for defining one-time bus-message scheduling semantics.

Quartz therefore comes first because its job/trigger model exists in both ecosystems and leaves envelope identity, transport dispatch, retry policy, and monitoring ownership visible to MyServiceBus. This is common architectural evidence, not a promise that Quartz is the common runtime. A later Hangfire adapter may deliberately project the same MyServiceBus command into Hangfire's richer state machine without changing the portable scheduler contract. JobRunr should be evaluated separately when the recurring-job model is designed. Applications needing a scheduler whose records and envelopes are intentionally shared by C# and Java should prefer the MyServiceBus PostgreSQL provider.

References:

- [Hangfire background method persistence and states](https://docs.hangfire.io/en/latest/background-methods/index.html)
- [Hangfire delayed jobs](https://docs.hangfire.io/en/latest/background-methods/calling-methods-with-delay)
- [Hangfire licensing](https://www.hangfire.io/licenses.html)
- [JobRunr architecture and storage](https://www.jobrunr.io/en/documentation/)
