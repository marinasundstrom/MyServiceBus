# Sagas and State Machines Proposal

## Status

Future product-area proposal. This document defines the intended feature boundary and investigation sequence; it is not yet a supported API or runtime commitment.

## Summary

Sagas and state machines should become one coherent MyServiceBus orchestration feature implemented in both C# and Java. The implementation should be based on MassTransit's proven saga architecture, primitives, execution order, and observable behavior while remaining a MyServiceBus-owned reimplementation with no MassTransit runtime dependency.

The complete feature has three distinct layers:

1. a language-neutral saga model, behavior specification, topology projection, and conformance suite;
2. native C# and Java runtimes with corresponding library-based state-machine DSLs; and
3. optional language projections, including a future Raven `saga!` macro that lowers into the same runtime model.

The C# and Java DSLs are first-class product APIs. They must not be implemented in terms of the Raven macro, require compiler macros, or require code generation for correctness. Generated registration may optimize either client, but an explicit runtime registration path remains supported.

## Product Boundary

A saga is a durable coordinator for one long-running workflow instance. It correlates multiple messages, remembers process state, directs subsequent work, handles time and failure, and eventually completes. A saga state machine declares that coordinator as states, events, state-specific behaviors, activities, and transitions.

This is orchestration. It is separate from choreography, where independently owned services react to events without a central owner of business-process state.

The complete state-machine feature should include:

- durable saga instances and repositories;
- identity and query correlation with explicit provider capabilities;
- initial, state-specific, any-state, and final behavior;
- ordered synchronous and asynchronous activities;
- send, publish, respond, request, schedule, and unschedule activities;
- guards, ignores, faults, retries, and missing-instance policies;
- transitions, finalization, and completed-instance retention or deletion;
- composite events or an equivalent deterministic join primitive;
- optimistic and pessimistic concurrency capabilities;
- transactional outbox integration;
- normalized topology and application-oriented monitoring; and
- equivalent C# and Java conformance evidence.

Consumer sagas, routing slips, compensation logs, and richer workflow-authoring tools are related but separable profiles. They should not enter the first state-machine runtime accidentally merely because MassTransit also supports them.

## MassTransit as the Baseline

MyServiceBus should study MassTransit's implementation as the primary architectural reference instead of inventing an unrelated saga engine. The reimplementation should preserve its familiar concepts and fundamental behavior when they fit the cross-language product:

- saga instance and correlation identity;
- `State` and `Event<T>`;
- `Initial` and `Final` pseudo-states;
- `Initially`, `During`, `DuringAny`, and `When`;
- ordered activities such as `Then`, `Publish`, `Send`, and `Respond`;
- `TransitionTo`, `Finalize`, and completed-instance policy;
- `Ignore` and missing-instance behavior;
- identity correlation, query correlation, and new-instance ID selection;
- schedules, requests, and their outcome events;
- composite events;
- exception activities and fault propagation;
- repository concurrency behavior; and
- outbox behavior when persistence fails.

Behavioral familiarity matters more than reproducing every overload or internal class. Any divergence should be deliberate, documented, represented as a capability where necessary, and covered by C# and Java fixtures. Directly incorporated source must retain all required notices and license treatment; otherwise, the goal is an architectural and behavioral reimplementation using MyServiceBus-owned code.

## Shared Definition

Both clients should normalize their language DSL into the same conceptual definition:

```text
Saga definition
├─ stable saga and state-machine identity
├─ saga-data contract and current-state field
├─ endpoint and repository requirements
├─ states
├─ events
│  ├─ message contract
│  ├─ correlation rule
│  ├─ creation and missing-instance policy
│  └─ topology relationship
└─ behaviors
   ├─ accepted source states
   ├─ event
   ├─ ordered activities
   └─ transition or finalization outcome
```

The portable definition contains stable identities, declared relationships, operation kinds, and provider requirements. It does not serialize native types, expressions, delegates, lambdas, dependency providers, ORM sessions, or executable application code.

Each runtime associates local callbacks and native types with this definition. Registration, validation, endpoint topology, execution, inspection, and monitoring must consume the same normalized declaration rather than rediscovering separate models.

## Native Library DSLs

C# and Java should each provide a normal library API for defining state machines. The APIs should be recognizably related and map unambiguously to the shared definition while using the host language's type system and asynchronous conventions.

An exploratory C# shape may resemble:

```csharp
public sealed class OrderStateMachine : SagaStateMachine<OrderState>
{
    public State AwaitingPayment { get; } = State(nameof(AwaitingPayment));
    public State Processing { get; } = State(nameof(Processing));

    public Event<OrderSubmitted> OrderSubmitted { get; } = Event<OrderSubmitted>();
    public Event<PaymentReceived> PaymentReceived { get; } = Event<PaymentReceived>();

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);
        CorrelateById(OrderSubmitted, x => x.Message.OrderId)
            .SelectId(x => x.Message.OrderId);
        CorrelateById(PaymentReceived, x => x.Message.OrderId);

        Initially(
            When(OrderSubmitted)
                .Send(x => new ReserveInventory(x.Message.OrderId))
                .TransitionTo(AwaitingPayment));

        During(AwaitingPayment,
            When(PaymentReceived)
                .TransitionTo(Processing));
    }
}
```

An exploratory Java shape may resemble:

```java
public final class OrderStateMachine extends SagaStateMachine<OrderState> {
    private final State awaitingPayment = state("AwaitingPayment");
    private final State processing = state("Processing");

    private final Event<OrderSubmitted> orderSubmitted = event(OrderSubmitted.class);
    private final Event<PaymentReceived> paymentReceived = event(PaymentReceived.class);

    public OrderStateMachine() {
        instanceState(OrderState::getCurrentState, OrderState::setCurrentState);
        correlateById(orderSubmitted, x -> x.message().orderId())
            .selectId(x -> x.message().orderId());
        correlateById(paymentReceived, x -> x.message().orderId());

        initially(
            when(orderSubmitted)
                .send(x -> new ReserveInventory(x.message().orderId()))
                .transitionTo(awaitingPayment));

        during(awaitingPayment,
            when(paymentReceived)
                .transitionTo(processing));
    }
}
```

These examples establish direction, not final signatures. The design must validate async activities, cancellation, exceptions, Java type erasure, source generation, annotation processing, NativeAOT, and GraalVM constraints before stabilization. The equivalent declaration must also be constructible explicitly through descriptors or builders so code generation is optional.

## Runtime Behavior

For each delivery, both runtimes should follow the same observable stages:

1. identify the saga definition and event from the concrete contract;
2. calculate the correlation query or instance identity;
3. load an instance or apply the event's creation or missing-instance policy;
4. select one matching behavior from the persisted state;
5. execute ordered activities in a scoped saga context;
6. record data changes, transitions, schedules, and outgoing envelopes;
7. atomically persist state and transactionally captured outgoing work where supported;
8. acknowledge the delivery only after the selected durability boundary succeeds; and
9. emit bounded transition, fault, repository, topology, and health observations.

Retry after a repository conflict must reload the instance and reevaluate behavior. Outgoing messages must not escape before a failed state update is known. Duplicate and out-of-order events require explicit outcomes rather than accidental exceptions.

## Persistence, Scheduling, and Monitoring

Repository capabilities must state whether they support identity correlation, query correlation, optimistic concurrency, pessimistic concurrency, transactions, atomic outbox participation, final-instance deletion, and schema evolution. An in-memory repository is a volatile development provider, not production workflow state.

Scheduled events, requests, and composite events belong to the shared state-machine model. They must reuse MyServiceBus scheduling, request, fault, and outbox primitives rather than creating parallel infrastructure.

Topology should expose saga/state-machine identity, states, consumed and produced contracts, endpoint attachment, correlation shape, and persistence requirements without executable callbacks. Monitoring should cover state distribution, transitions, correlation failures, missing instances, repository conflicts, schedules, requests, faults, provider health, freshness, and completeness without exporting saga payloads by default.

## Raven Projection

The future [Raven Saga DSL Exploration](raven-saga-dsl.md) is an optional compiler projection over this feature. A Raven `saga!` declaration should lower into the same MyServiceBus definition and runtime used by the ordinary C# library DSL. Once the shared infrastructure is stable, this projection can provide a more approachable, workflow-oriented authoring experience than either native fluent API while preserving those C# and Java APIs as complete alternatives.

The macro must not be required to define or execute a C# or Java state machine, construct the portable definition, persist or monitor instances, register state machines without generation, or pass the shared conformance suite. Its value is readable domain-order syntax, Raven-native static analysis, generated registration, stronger diagnostics, and executable documentation; it is not the architectural center of the saga feature.

## Cross-Language Evidence

Canonical definition and delivery-sequence fixtures should drive matching C# and Java tests for creation, correlation, transitions, ignores, missing instances, duplicates, out-of-order delivery, finalization, concurrency conflicts, outbox atomicity, scheduling, requests, composite events, activity faults, topology, and monitoring.

Broker tests should prove that C# and Java services exchange every saga input and output contract bidirectionally. Sharing message contracts is required; sharing one live saga instance across runtimes is claimed only by a separately specified interoperable repository profile.

## Recommended Sequence

1. Study the MassTransit 8.5.1 saga implementation and tests and write the portable behavior specification.
2. Define canonical state-machine and failure-sequence fixtures.
3. Define the normalized declaration, repository capabilities, topology, and monitoring contracts.
4. Implement the smallest in-memory C# and Java runtimes with their native library DSLs.
5. Integrate transactional outbox and durable scheduling.
6. Add one durable repository provider in each ecosystem and run restart and concurrency gates.
7. Add requests, scheduled events, composite events, and richer activities through shared fixtures.
8. Build the Raven `saga!` sample over the ordinary .NET runtime.
9. Promote only after cross-language, broker, persistence, recovery, and monitoring evidence passes on one commit.

## Open Questions

1. Which MassTransit version is the normative implementation-study baseline?
2. Should the first release include consumer sagas or only declarative state machines?
3. Which correlation queries can providers support portably?
4. How is current state encoded and versioned for in-flight definition upgrades?
5. Which activity extension model remains safe and idiomatic in both languages?
6. Which durable provider should become the first production candidate?
7. When, if ever, should one persisted instance be executable interchangeably by C# and Java?

## References

- [MassTransit saga state-machine concepts](https://masstransit.massient.com/concepts/saga-state-machines/)
- [MassTransit 8.5.1 source](https://github.com/MassTransit/MassTransit/tree/v8.5.1)
- [Raven Saga DSL Exploration](raven-saga-dsl.md)
- [Topology Extension Model](../specs/topology-extension-model.md)
- [Transactional Outbox and Inbox Specification](../specs/outbox-inbox.md)
- [MyServiceBus Design Goals](../development/design-goals.md)
