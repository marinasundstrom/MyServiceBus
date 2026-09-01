# Saga State-Machine Behavior

## Status

This specification defines the portable behavior targeted by the first MyServiceBus saga state-machine implementation. It is an implementation contract for matching C# and Java runtimes, not yet a supported public API commitment.

MassTransit 8.5.1 is the architectural and behavioral baseline. MyServiceBus owns its implementation and public APIs; compatibility means preserving recognizable concepts and deliberate observable behavior, not depending on MassTransit or reproducing its internal types.

## Scope

A saga state machine is a centralized coordinator for one durable workflow instance. An incoming message is correlated to an instance, one behavior is selected from its persisted state, activities run in declaration order, and the resulting state and outgoing work cross a defined durability boundary.

The first executable profile is intentionally smaller than the complete feature:

- one stable state-machine definition and saga-data identity;
- an implicit `Initial` pseudo-state, named ordinary states, and an implicit `Final` pseudo-state;
- typed message events with identity correlation;
- creation by explicitly declared initial events;
- existing-instance delivery for subsequent events;
- `Initially`, `During`, `DuringAny`, `When`, and `Ignore` behavior;
- ordered mutation, send, publish, transition, and finalize activities;
- an in-memory repository with explicit volatile durability; and
- normalized declarations and matching C# and Java fixtures.

Query correlation, durable repositories, conflict retries, transactional outbox integration, schedules, requests, responses, composite events, exception activities, and multiple named final outcomes are specified as later profiles. Their place in the model is reserved, but the first runtime must not claim those capabilities.

## Abstraction Layers

This behavior is exposed at several levels without making any one syntax authoritative:

1. The portable specification and normalized declaration define shared meaning, validation, topology, and conformance evidence.
2. Low-level C# and Java runtime APIs implement correlation, instance access, behavior selection, ordered activities, persistence, and observations.
3. Native C# and Java state-machine DSLs build executable definitions using those primitives. Their shapes may differ by language; the C# form may remain deliberately familiar to MassTransit users.
4. Raven's future `saga!` macro generates artifacts for the same .NET declaration and runtime rather than introducing another saga engine.

The compatibility target is the state-machine concepts and observable behavior in this specification. Exact MassTransit public interfaces, base classes, generic constraints, overloads, and fluent signatures are not compatibility requirements. The low-level APIs also should not be mistaken for the preferred application-facing DSL merely because they are implemented first.

## Current Implementation Boundary

The first matching C# and Java low-level runtimes now execute the version 1 subset against volatile in-memory repositories. Native callbacks bind message types, correlation selectors, saga factories and cloning, state accessors, mutations, and outgoing message factories to a validated normalized definition. The runtimes serialize work per correlation identity, execute activities in order against a cloned working instance, commit only after the behavior succeeds, and return logically captured send or publish operations with the transition result.

This is execution-foundation evidence, not yet a supported bus-integrated saga feature. No receive endpoint is registered automatically, captured outgoing work is not dispatched to a broker, and the in-memory logical capture is not a durable transactional outbox. Process loss discards all instances. The low-level activity-index binding is intended for adapters, generators, and the future native DSL implementations rather than ordinary application authoring.

## Definitions

- **Definition**: the immutable state-machine declaration used for validation, execution, topology, and monitoring.
- **Saga instance**: application-owned persisted data for one workflow, including a stable correlation ID and current state.
- **Event**: a message contract recognized by the state machine.
- **Behavior**: the event handling selected for one source state.
- **Activity**: one ordered operation within a behavior.
- **Initial event**: an event that is allowed to create a missing instance and has behavior in `Initial`.
- **Finalization**: transition to the `Final` pseudo-state. Removal or retention is a separate completion policy.

`Initial` and `Final` are reserved pseudo-state identities and cannot be redeclared as ordinary states. A newly constructed instance behaves as if it is in `Initial`, even if its stored state field is unset. `DuringAny` applies only to ordinary states in the first profile; it cannot accidentally create an instance or revive a completed one.

## Portable Declaration

The normalized declaration contains data, not executable application code:

```text
State-machine definition
├─ schema version, stable ID, definition version, owner
├─ saga-data message URN and current-state member identity
├─ completion policy and repository requirements
├─ named ordinary states
├─ events
│  ├─ stable event ID and message URN
│  ├─ correlation kind and member identities
│  ├─ creation policy
│  └─ missing-instance policy
└─ behaviors
   ├─ source-state selector and event ID
   └─ ordered activity descriptors
```

Native message types, property expressions, delegates, lambdas, dependency containers, repository sessions, and activity callbacks stay in the local runtime. Stable member identities describe their meaning for validation and inspection; they are not expressions executed from a JSON fixture.

The canonical fixture at `test/fixtures/state-machines/v1/basic-order-state-machine.json` defines the serialized ordering and vocabulary for version 1 declarations. `basic-order-sequence.json` defines matching observable creation, transition, outgoing-operation, finalization, deletion, and missing-discard outcomes. Arrays are ordered deterministically by stable ID except that activities retain declaration order because order is behavioral.

## Validation

A definition is rejected before endpoint startup when any of these conditions is known:

- its stable ID, version, owner, saga-data URN, or state member is empty;
- a state or event ID is duplicated;
- a user state uses the reserved `Initial` or `Final` identity;
- an event references an unsupported correlation, creation, or missing-instance policy;
- an event can create an instance but has no `Initial` behavior;
- an `Initial` behavior uses an event that cannot create an instance;
- a behavior references an unknown state or event;
- more than one unconditional behavior exists for the same state and event;
- an activity references an unknown state or lacks operation-specific data;
- an activity follows `transition` or `finalize` in the same behavior; or
- the declared repository requirements exceed provider capabilities.

The first profile permits exactly one behavior for a state/event pair. Guarded alternatives will require deterministic ordering and ambiguity rules before they are added.

## Delivery Algorithm

For each recognized event delivery, both runtimes perform these observable stages:

1. Resolve the event by its concrete message contract.
2. Evaluate its correlation rule and obtain the saga identity.
3. Load the instance from the configured repository.
4. If it is missing, apply the event's creation or missing-instance policy.
5. Determine the effective current state (`Initial` for a new or unset instance).
6. Select the behavior for that state and event, considering ordinary state behavior before `DuringAny`.
7. Execute its activities once, sequentially, in declaration order.
8. Persist the insert, update, completion, and captured outgoing work at the provider's declared durability boundary.
9. Acknowledge the incoming delivery only after that boundary succeeds.
10. Emit bounded transition or fault observations without saga or message payloads by default.

An implementation may organize its internal pipeline differently, but it must not reorder these externally meaningful effects.

## Correlation and Creation

The first profile supports identity correlation: a declared message member produces the same GUID identity stored as the saga correlation ID. Empty or invalid identities fail before repository access.

Creation is explicit per event:

- `if-missing` permits creation only when the selected behavior is in `Initial`;
- `existing-only` never creates an instance;
- the creator supplies the new saga identity through the declared ID selector; and
- a newly created instance receives the initiating event exactly once in `Initial` before its first insert is committed.

If an `if-missing` delivery races with another creator, a durable provider must resolve the unique-identity conflict through its concurrency policy. It must not retain two instances. The in-memory profile may serialize operations by identity but must expose that it is not a cross-process concurrency guarantee.

Query correlation is a later provider capability. When introduced, the match must be zero or one instance; multiple matches are an ambiguous-correlation fault, not an arbitrary selection.

## Missing Instances

An `existing-only` event that finds no instance applies its declared policy:

- `discard`: acknowledge without state or outgoing work;
- `fault`: fail the delivery through the normal fault and retry pipeline; or
- `execute`: run a separately declared missing-instance action without manufacturing a saga instance.

The first executable profile implements `discard` and `fault`. `execute` is reserved for a later profile because its portable outgoing-work and durability semantics must be specified first.

Missing-instance handling and unhandled-event handling are distinct: the former has no instance; the latter found an instance but has no accepted behavior in its current state.

## Behavior Selection

`Initially` binds behavior to `Initial`. `During(state, ...)` binds behavior to the named ordinary state. `DuringAny` provides fallback behavior for ordinary states only.

For one event and effective state:

1. an exact state behavior wins;
2. otherwise a `DuringAny` behavior is selected;
3. otherwise an explicit ignore succeeds; and
4. otherwise the event is unhandled.

An ignored event is accepted without running activities, changing state, creating outgoing work, or treating the delivery as a failure. An unhandled event raises a state-machine not-accepted fault containing definition identity, event identity, correlation ID, and current state. It uses the ordinary retry and failed-message path.

Events delivered to a retained `Final` instance are unhandled unless a later profile explicitly defines final-state inspection behavior. A removed completed instance instead follows the event's missing-instance policy.

## Activity Ordering and Failure

Activities form one sequential behavior pipeline. Each successful activity invokes the next; no later activity begins before the current activity completes.

The first profile defines:

- `mutate`: invoke a local typed callback that changes saga data;
- `send`: create one targeted outgoing message;
- `publish`: create one published outgoing message;
- `transition`: leave the current state and enter one named ordinary state; and
- `finalize`: leave the current state and enter `Final`.

`transition` and `finalize` terminate the activity list in version 1. A definition cannot rely on effects declared after either operation.

If an activity fails, later activities do not run. The incoming delivery fails. Durable profiles must roll back the instance mutation and withhold all captured outgoing messages from that attempt. An in-memory implementation must provide the same logical outcome within its process even though it cannot provide crash durability.

On retry, the runtime reloads the authoritative instance and reevaluates behavior. It does not resume in the middle of the failed activity list.

## Transitions and Completion

A transition updates the current state as part of the same unit of work as its other activities. Monitoring records the begin state, triggering event, successful activities, and end state only after persistence succeeds.

`finalize` enters the implicit `Final` pseudo-state. The completion policy then decides repository treatment:

- `retain`: persist the instance in `Final`; or
- `delete-when-finalized`: remove it at the successful durability boundary.

This separation follows the MassTransit distinction between reaching `Final` and configuring completed-instance removal. The first in-memory profile supports both policies.

The Raven exploration's `final Completed` and `final Cancelled` syntax represents a deliberate future extension for named terminal outcomes. Before implementation, it must define whether those outcomes are retained terminal states or labels carried into the single runtime `Final` state. Version 1 does not silently choose between those meanings.

## Concurrency, Retry, and Outbox Requirements

The in-memory repository is suitable for deterministic tests and development only. A durable provider cannot be promoted until it declares and proves:

- unique saga identity;
- optimistic or pessimistic concurrency behavior;
- conflict detection and bounded retry;
- atomic instance insert, update, or delete;
- atomic capture of outgoing work through the transactional outbox; and
- restart behavior for committed and uncommitted transitions.

A concurrency retry reloads current state and reevaluates correlation, behavior, and activities. Application callbacks may therefore run more than once. Outgoing messages from losing or failed attempts must not escape. Exactly-once application side effects are not implied; side effects outside the repository/outbox transaction remain application responsibilities.

## Later Profiles

These capabilities extend the same delivery algorithm rather than forming separate workflow engines:

- query correlation and custom missing-instance actions;
- guards and conditional branches;
- durable schedule and unschedule activities;
- request declarations with pending, completed, faulted, and timeout events;
- response activities preserving request correlation;
- composite events or another deterministic durable join;
- typed exception activities and retry blocks;
- multiple terminal outcomes;
- durable repository providers and schema evolution; and
- retained transition history and live instance monitoring.

For request/response monitoring, the request and correlated return remain one paired interaction. The saga state shows the authoritative pending request, while fault and timeout are alternative returns rather than choreography forks.

## Conformance Evidence

C# and Java implementations must run equivalent cases for:

1. definition normalization against the canonical fixture;
2. initial creation and transition;
3. existing-instance correlation and transition;
4. ignored versus unhandled events;
5. missing-instance discard and fault;
6. activity order and short-circuit on failure;
7. finalization with retain and delete policies;
8. duplicate initial delivery and identity collision;
9. invalid-definition diagnostics; and
10. topology and monitoring projection without payload leakage.

Durable providers add restart, conflict, rollback, and outbox-release cases. Broker interoperability proves only the messages crossing the saga boundary; it does not imply a shared MassTransit/MyServiceBus saga repository.

## MassTransit Baseline Notes

The version 1 rules are based on observable behavior in MassTransit 8.5.1's state-machine definition, event-correlation configuration, saga policies, message filter, activities, and focused tests. In particular:

- `Initially` binds through `Initial`;
- `DuringAny` excludes ordinary handling in `Initial` and `Final`;
- ignored events suppress the unhandled-event fault;
- unhandled events become state-machine not-accepted failures;
- activities invoke their successor in declaration order;
- completion is evaluated after event execution; and
- final-state deletion requires a configured completion predicate.

MyServiceBus deliberately starts with one portable subset. Differences and later extensions must be recorded here and covered by matching C# and Java evidence.

## References

- [MassTransit saga state machines](https://masstransit.io/documentation/patterns/saga/state-machine)
- [MassTransit 8.5.1 source](https://github.com/MassTransit/MassTransit/tree/v8.5.1)
- [Sagas and State Machines Proposal](../proposals/sagas-and-state-machines.md)
- [Raven Saga DSL Exploration](../proposals/raven-saga-dsl.md)
- [Topology Extension Model](topology-extension-model.md)
