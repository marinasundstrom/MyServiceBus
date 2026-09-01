# Sagas and State Machines Proposal

## Status

Active implementation proposal. The portable version 1 behavior is now defined in the [Saga State-Machine Behavior specification](../specs/saga-state-machine-behavior.md); no saga API or runtime is supported yet.

## Summary

Sagas and state machines provide orchestration: centralized workflow coordination in which one component owns the broader process state and decides how participants should be directed through messages. “Centralized” refers to ownership of workflow knowledge and decisions, not to a single deployment or synchronous execution.

Applications can implement an orchestrator directly with consumers, messages, persistence, and application-owned state. The proposed runtime and DSLs provide safer, more concise C# and Java abstractions for expressing that intent while preserving the same application ownership of business rules.

Sagas and state machines should become one coherent MyServiceBus orchestration feature implemented in both C# and Java. The implementation should be based on MassTransit's proven saga architecture, primitives, execution order, and observable behavior while remaining a MyServiceBus-owned reimplementation with no MassTransit runtime dependency.

The complete feature has four distinct layers:

1. a language-neutral saga model, behavior specification, topology projection, and conformance suite;
2. low-level C# and Java runtime APIs for correlation, repositories, behavior selection, activities, persistence, and observations;
3. native library-based state-machine DSLs that compile or build upon those low-level APIs; and
4. optional language projections, including a future Raven `saga!` macro that lowers into the same .NET runtime model.

The normalized descriptor builders and low-level runtime contracts are infrastructure APIs, not the final state-machine authoring experience. The C# and Java DSLs are first-class product APIs layered above them. They must not be implemented in terms of the Raven macro, require compiler macros, or require code generation for correctness. Generated registration may optimize either client, but an explicit runtime registration path remains supported.

MassTransit compatibility is primarily conceptual and behavioral. MyServiceBus is not required to reproduce MassTransit's interfaces, inheritance hierarchy, overload set, or exact fluent signatures. One native DSL—especially the C# DSL—may intentionally resemble MassTransit's established `Initially`/`During`/`When` model because it is expressive and familiar, while Java and other projections can use language-appropriate shapes. Every abstraction level must still lower to the same MyServiceBus semantics and produce equivalent observable outcomes.

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

## Protocol Compatibility and Innovation Boundary

Saga execution sits above the messaging protocol. MyServiceBus may define its own C#, Java, and Raven authoring experiences plus its own runtime, repository, topology, and monitoring model while retaining MassTransit-compatible message contracts, envelopes, addressing, correlation headers, send and publish behavior, requests, responses, faults, and transport profiles.

A MassTransit service can therefore consume commands or events emitted by a MyServiceBus saga and send correlated messages back, and a MyServiceBus service can participate in a workflow initiated by MassTransit. Neither side needs the other's state-machine DSL for message interoperability.

That compatibility does not by itself make saga state portable. A MassTransit saga and a MyServiceBus saga do not share one live instance, repository record, scheduler token, or transition history unless a separate, explicit interoperability profile defines and verifies those boundaries. MyServiceBus can innovate above the wire while keeping every claimed protocol interaction covered by transport and cross-language conformance tests.

The state-machine definition and executable activities are local to the coordinating service. Other services see only the messages it consumes and produces; they do not call its DSL or inspect its repository. The language-neutral definition gives the C# and Java clients aligned concepts, fixtures, topology, and monitoring, but it is not a new cross-service invocation protocol.

Persistence, concurrency, retry, scheduling, and outbox behavior still require portable specification because they affect externally visible message outcomes after crashes, conflicts, duplicates, and timeouts. Internal freedom is compatible with protocol interoperability only when those observable outcomes remain deliberate and tested.

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

These authoring DSLs sit above the low-level execution and descriptor APIs. Application developers may use the lower level directly for framework integration, generated definitions, testing, or unusual dynamic composition, but ordinary applications should not need to assemble repository policies, transition tables, or activity pipelines manually.

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

These examples establish direction, not final signatures or an API-compatibility promise. The design must validate async activities, cancellation, exceptions, Java type erasure, source generation, annotation processing, NativeAOT, and GraalVM constraints before stabilization. The equivalent declaration must also be constructible explicitly through descriptors or builders so code generation is optional.

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

The dashboard should present orchestration and choreography through a related workflow vocabulary without pretending that their evidence is equivalent. An orchestrated saga has a framework-owned instance identity and persisted current state, so the dashboard can authoritatively show where that instance is, how it arrived there, what it is waiting for, and whether it completed or faulted. A choreographed flow is reconstructed from distributed declarations and bounded observations and must retain confidence and coverage labels. Shared maps, timelines, contract nodes, causal message edges, and application drill-downs can make the two styles comparable while state badges and lifecycle claims remain specific to orchestration.

The orchestration drill-down should have a definition graph and an instance timeline. The graph renders initial, ordinary, and final states plus event-labeled transitions, keeps the full definition visible, highlights the selected instance's current state, marks its traversed path, and annotates pending timeouts or requests. Aggregate mode can place instance count, oldest age, transition rate, and fault count on the relevant states and edges. Selecting a graph element filters the ordered transition records rather than placing full operational detail inside nodes.

Within one transition's activity view, a request and response should be rendered as one paired interaction: a solid request edge to the responder and a dashed correlated return edge to the waiting activity. Its detail separates outbound handoff, responder execution, response handoff, and total round-trip time, while fault and timeout outcomes remain alternate results of that same request. The state-machine graph should additionally show the authoritative pending request on the owning saga state; it should not misrepresent the response as an unrelated choreography fork.

Each recorded transition should identify the definition and instance, previous and next state, triggering event contract, time and duration, owning application and component, message and correlation references, activities and outgoing operation contracts, scheduled deadlines, attempt or repository-conflict outcome, and fault category. Saga data, message bodies, arbitrary headers, and raw exception messages remain excluded by default.

Workflow visibility remains read-only monitoring by default. Retry, skip, force-transition, compensate, or terminate operations would cross into a privileged control plane and require separate authorization, concurrency checks, confirmation, and audit semantics.

## Raven Projection

The future [Raven Saga DSL Exploration](raven-saga-dsl.md) is an optional compiler projection over this feature. A Raven `saga!` declaration should lower into the same MyServiceBus definition and runtime used by the ordinary C# library DSL. Once the shared infrastructure is stable, this projection can provide a more approachable, workflow-oriented authoring experience than either native fluent API while preserving those C# and Java APIs as complete alternatives.

The macro must not be required to define or execute a C# or Java state machine, construct the portable definition, persist or monitor instances, register state machines without generation, or pass the shared conformance suite. Its value is readable domain-order syntax, Raven-native static analysis, generated registration, stronger diagnostics, and executable documentation; it is not the architectural center of the saga feature.

## Cross-Language Evidence

Canonical definition and delivery-sequence fixtures should drive matching C# and Java tests for creation, correlation, transitions, ignores, missing instances, duplicates, out-of-order delivery, finalization, concurrency conflicts, outbox atomicity, scheduling, requests, composite events, activity faults, topology, and monitoring.

Broker tests should prove that C# and Java services exchange every saga input and output contract bidirectionally. Sharing message contracts is required; sharing one live saga instance across runtimes is claimed only by a separately specified interoperable repository profile.

## Recommended Sequence

1. Study the MassTransit 8.5.1 saga implementation and tests and write the portable behavior specification. **Completed for the version 1 subset.**
2. Define canonical state-machine and failure-sequence fixtures.
3. Define the normalized declaration, repository capabilities, topology, and monitoring contracts.
4. Implement the smallest in-memory C# and Java runtimes with their native library DSLs.
5. Integrate transactional outbox and durable scheduling.
6. Add one durable repository provider in each ecosystem and run restart and concurrency gates.
7. Add requests, scheduled events, composite events, and richer activities through shared fixtures.
8. Build the Raven `saga!` sample over the ordinary .NET runtime.
9. Promote only after cross-language, broker, persistence, recovery, and monitoring evidence passes on one commit.

## Open Questions

1. Should the first release include consumer sagas or only declarative state machines?
2. Which correlation queries can providers support portably?
3. How is current state encoded and versioned for in-flight definition upgrades?
4. Which activity extension model remains safe and idiomatic in both languages?
5. Which durable provider should become the first production candidate?
6. When, if ever, should one persisted instance be executable interchangeably by C# and Java?

## References

- [MassTransit saga state machines](https://masstransit.io/documentation/patterns/saga/state-machine)
- [MassTransit 8.5.1 source](https://github.com/MassTransit/MassTransit/tree/v8.5.1)
- [Saga State-Machine Behavior](../specs/saga-state-machine-behavior.md)
- [Raven Saga DSL Exploration](raven-saga-dsl.md)
- [Topology Extension Model](../specs/topology-extension-model.md)
- [Transactional Outbox and Inbox Specification](../specs/outbox-inbox.md)
- [MyServiceBus Design Goals](../development/design-goals.md)
