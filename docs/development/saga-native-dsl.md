# Native Saga State-Machine DSL

## Status

Design and acceptance contract for the first C# and Java authoring DSLs. The fundamental APIs illustrated here now lower to the shared normalized definition and provider-neutral repository runtime in both clients. An in-memory provider supports local development, while the matching PostgreSQL providers persist saga JSON, serialize correlation-scoped execution, delete finalized instances, and commit state with outgoing messages through the transactional outbox. Experimental registration attaches every declared event to one receive endpoint. Matching focused tests and a mixed C#/Java Aspire order workflow demonstrate the vertical slice. Definition topology, bounded committed-transition monitoring, and an initial Dashboard instance view are implemented; durable lifecycle retention and broader failure/recovery validation remain before the feature is production-capable.

## Design Goal

The native DSLs should make the common saga path read like an Automatonymous state machine while remaining ordinary host-language libraries. C# preserves substantial familiarity with MassTransit's current state-machine model. Java preserves the same ordering and concepts through JVM naming, functional interfaces, and `CompletionStage` rather than reproducing C# syntax.

Compatibility targets concepts and observable behavior. It does not require the MassTransit inheritance hierarchy, every historical overload, source compatibility, or a dependency on MassTransit. MyServiceBus may improve validation, identity, generation, AOT behavior, monitoring, and separation between authoring and runtime APIs.

## Usable MVP

The immediate target is a usable vertical slice, not a comprehensive state-machine language. A developer must be able to:

1. define the fundamental machine below through the native C# or Java DSL;
2. register the machine and an in-memory repository with ordinary MyServiceBus configuration;
3. receive its initiating and subsequent events from a real receive endpoint;
4. correlate, create, load, transition, finalize, and remove process-local saga instances;
5. dispatch its captured send and publish activities through the existing scoped bus pipeline;
6. observe faults through the ordinary retry, fault, and failed-message behavior; and
7. run documented focused and Aspire samples that demonstrate the lifecycle.

The MVP includes no durable-state promise. Its documentation and health metadata must label the repository volatile and explain that process restart loses active workflows. Query correlation, requests, durable schedules, composite events, hierarchical states, exception branches, durable repositories, and the Raven macro follow the working vertical slice.

Two samples should lead the experience:

- a small in-memory sample and test that makes the DSL, transitions, ignores, and finalization easy to understand; and
- an Aspire order-orchestration sample with an orchestrator plus independently deployed payment and inventory participants exchanging ordinary MassTransit-compatible messages.

Only one language implementation should own a saga instance in a running distributed sample. Equivalent C# and Java definitions and conformance tests demonstrate client parity; they must not run as competing coordinators for the same correlation identity.

## Fundamental Acceptance Machine

The first DSL must express one order coordinator with:

- application-owned saga data;
- explicit state storage, instance creation, and cloning;
- `AwaitingPayment` and `Processing` states;
- initiating `OrderSubmitted` identity correlation;
- existing-only `PaymentReceived` and `ProcessingCompleted` correlation;
- ordered mutation, send, publish, transition, and finalize activities;
- exact-state behavior, with `DuringAny` ignore covered by a supplementary conformance machine;
- missing-instance discard and fault policies; and
- delete-on-finalized completion.

An intended C# shape is:

```csharp
public sealed class OrderStateMachine : SagaStateMachine<OrderState>
{
    public State AwaitingPayment { get; }
    public State Processing { get; }

    public Event<OrderSubmitted> OrderSubmitted { get; }
    public Event<PaymentReceived> PaymentReceived { get; }
    public Event<ProcessingCompleted> ProcessingCompleted { get; }

    public OrderStateMachine()
        : base("order-state-machine", "1", "orders")
    {
        InstanceState(state => state.CurrentState, (state, value) => state.CurrentState = value);
        InstanceFactory(id => new OrderState { CorrelationId = id });
        CloneInstance(state => state.Copy());

        AwaitingPayment = State(nameof(AwaitingPayment));
        Processing = State(nameof(Processing));

        OrderSubmitted = Event<OrderSubmitted>(
            nameof(OrderSubmitted),
            "urn:message:Contracts:OrderSubmitted",
            correlation => correlation
                .CorrelateById("CorrelationId", "OrderId", message => message.OrderId)
                .CreatesIfMissing()
                .FaultIfMissing());

        PaymentReceived = Event<PaymentReceived>(
            nameof(PaymentReceived),
            "urn:message:Contracts:PaymentReceived",
            correlation => correlation
                .CorrelateById("CorrelationId", "OrderId", message => message.OrderId)
                .DiscardIfMissing());

        ProcessingCompleted = Event<ProcessingCompleted>(
            nameof(ProcessingCompleted),
            "urn:message:Contracts:ProcessingCompleted",
            correlation => correlation
                .CorrelateById("CorrelationId", "OrderId", message => message.OrderId));

        Initially(
            When(OrderSubmitted)
                .Then(context => context.Saga.OrderId = context.Message.OrderId)
                .Send(
                    "urn:message:Contracts:ReserveInventory",
                    "queue:reserve-inventory",
                    context => new ReserveInventory(context.Saga.OrderId))
                .TransitionTo(AwaitingPayment));

        During(AwaitingPayment,
            When(PaymentReceived)
                .Then(context => context.Saga.PaymentReceived = true)
                .TransitionTo(Processing));

        During(Processing,
            When(ProcessingCompleted)
                .Publish(
                    "urn:message:Contracts:OrderCompleted",
                    context => new OrderCompleted(context.Message.OrderId))
                .Finalize());

        DeleteWhenFinalized();
    }
}
```

The corresponding Java shape is intentionally related rather than textually identical:

```java
public final class OrderStateMachine extends SagaStateMachine<OrderState> {
    private final State awaitingPayment;
    private final State processing;

    private final Event<OrderSubmitted> orderSubmitted;
    private final Event<PaymentReceived> paymentReceived;
    private final Event<ProcessingCompleted> processingCompleted;

    public OrderStateMachine() {
        super("order-state-machine", "1", "orders", "urn:message:Contracts:OrderState");

        instanceState(OrderState::currentState, OrderState::setCurrentState);
        instanceFactory(OrderState::new);
        cloneInstance(OrderState::copy);

        awaitingPayment = state("AwaitingPayment");
        processing = state("Processing");

        orderSubmitted = event(
                "OrderSubmitted",
                "urn:message:Contracts:OrderSubmitted",
                OrderSubmitted.class,
                correlation -> correlation
                        .correlateById("CorrelationId", "OrderId", OrderSubmitted::orderId)
                        .createsIfMissing()
                        .faultIfMissing());

        paymentReceived = event(
                "PaymentReceived",
                "urn:message:Contracts:PaymentReceived",
                PaymentReceived.class,
                correlation -> correlation
                        .correlateById("CorrelationId", "OrderId", PaymentReceived::orderId)
                        .discardIfMissing());

        processingCompleted = event(
                "ProcessingCompleted",
                "urn:message:Contracts:ProcessingCompleted",
                ProcessingCompleted.class,
                correlation -> correlation
                        .correlateById("CorrelationId", "OrderId", ProcessingCompleted::orderId));

        initially(
                when(orderSubmitted)
                        .then(context -> context.saga().setOrderId(context.message().orderId()))
                        .send(
                                "urn:message:Contracts:ReserveInventory",
                                "queue:reserve-inventory",
                                context -> new ReserveInventory(context.saga().orderId()))
                        .transitionTo(awaitingPayment));

        during(awaitingPayment,
                when(paymentReceived)
                        .then(context -> context.saga().setPaymentReceived(true))
                        .transitionTo(processing));

        during(processing,
                when(processingCompleted)
                        .publish(
                                "urn:message:Contracts:OrderCompleted",
                                context -> new OrderCompleted(context.message().orderId()))
                        .finalizeSaga());

        deleteWhenFinalized();
    }
}
```

Sync conveniences may wrap the native asynchronous activity form. The executable core remains `ValueTask`-based in C# and `CompletionStage`-based in Java so asynchronous application work is not hidden behind blocking calls.

## Lowering Contract

The DSL builds one normalized definition and one set of local runtime bindings. It does not execute behavior while the constructor is running.

| DSL concept | Normalized declaration | Low-level runtime binding |
| --- | --- | --- |
| `State` / `state` | stable ordinary-state ID | no callback |
| `Event<T>` / `Event<T>` | event ID, message URN, policies, correlation member identities | message class and identity selector |
| instance state | state-member identity | getter and setter |
| instance factory | no executable value serialized | correlation-ID factory |
| clone function | volatile repository capability | working-copy callback |
| `Initially` | source state `Initial` | ordinary behavior table |
| `During` | exact source-state ID | ordinary behavior table |
| `DuringAny` | source selector `Any` | ordinary-state fallback only |
| `Then` / `then` | `mutate` plus stable generated activity ID | typed async callback |
| `Send` / `send` | message URN and destination | typed message factory |
| `Publish` / `publish` | message URN | typed message factory |
| `TransitionTo` | terminal ordered `transition` activity | state accessor update |
| `Finalize` / `finalizeSaga` | terminal ordered `finalize` activity | update to `Final` |
| `Ignore` / `ignore` | sole `ignore` activity | no callback |
| completion policy | `retain` or `delete-when-finalized` | repository mutation choice |

Activity indexes are an internal lowering detail. Application-facing APIs never ask developers to bind callbacks by numeric position. Generated activity IDs must be deterministic within a behavior so topology and monitoring remain stable when unrelated definitions change.

## Construction and Validation

A state machine is frozen when its definition or runtime is first requested. Mutation afterward fails. Construction must reject duplicate states, events, or behaviors; unbound event correlation; initial behavior without creation; transition to an unknown state; activity after transition, finalization, or ignore; missing instance factory, clone, or state accessor; and executable activities lacking native callbacks.

The same machine object supplies:

- its immutable normalized `SagaStateMachineDefinition` for inspection and topology; and
- a runtime factory accepting `ISagaRepository<TSaga>` or `SagaRepository<TSaga>`, which binds the stored native callbacks to the low-level executor.

The DSL does not own a singleton repository. Registration chooses repository lifetime and provider, validates its declared correlation, concurrency, durability, outbox, and final-deletion capabilities against the definition, and creates the consumer adapter. Provider factories resolve inside the active consumer scope so a durable repository can share its connection, transaction, and `OutboxSession` with saga activities. `InMemorySagaRepository` implements this same contract rather than using a private runtime path.

## Deliberate Initial Deviations

- Stable definition, state, event, behavior, and activity identities are explicit or deterministic because cross-language monitoring needs them.
- Provider capability validation is part of startup rather than an ORM-specific convention.
- C# and Java asynchronous forms are native to each platform.
- The first surface can be smaller than MassTransit's overload set while preserving the common composition model.
- Multiple named terminal outcomes, hierarchical states, conditional branches, requests, schedules, and composite events remain later profiles.
- Raven `saga!` lowers into the same definition and runtime factory; it does not subclass or reinterpret the native DSL at runtime.

## Acceptance Evidence

Before the fundamental DSL is complete, C# and Java tests must prove that:

1. the two examples normalize to the canonical order-state-machine fixture;
2. they execute the canonical delivery-sequence fixture;
3. sync and async mutation/message factories retain declaration order;
4. exact behavior wins before `DuringAny`;
5. `DuringAny` does not apply to `Initial` or `Final`;
6. ignored, unhandled, missing-discard, and missing-fault outcomes remain distinct;
7. activity failure rolls back the working instance and outgoing capture; and
8. outgoing dispatch failure prevents the volatile instance from committing in both clients; and
9. the DSL uses the existing low-level runtime rather than a parallel executor.

Bus registration, consume-context dispatch, focused tests, and the mixed Aspire order sample now satisfy the first runtime vertical slice. Both runtimes execute through matching public repository and transaction contracts, and capability validation rejects a volatile provider when a machine requires durable storage or a transactional outbox. The PostgreSQL provider is the first durable profile: it uses one database transaction for the correlation-scoped saga mutation and every send or publication captured by `OutboxSession`. `AddPostgreSqlSagaStateMachine` in C# and `PostgreSqlSagas.addSagaStateMachine` in Java select that profile without changing the state-machine DSL. RabbitMQ acceptance tests exercise both ownership directions: MassTransit initiates and observes while either a C# or Java saga coordinates a participant written in the other MyServiceBus client. Both clients also publish the normalized saga definition and endpoint attachment through topology and inspection and emit payload-free lifecycle observations after repository commit. The monitoring service exposes separate replica-aware definition and bounded instance-transition queries plus shared workflow catalog and run-index projections. The Dashboard renders saga definitions and a selected instance's current state and committed transition timeline without flattening that evidence into choreography. Durable lifecycle retention and richer graph analysis remain later gates over the same machine definition.
