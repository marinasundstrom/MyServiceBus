# Raven Saga DSL Exploration

## Status

Executable prototype under `test/Experiments/RavenSagaDsl`; neither the prototype nor this document is a supported saga API commitment or a commitment to final Raven syntax.

Target: a Raven-authored executable sample built on MyServiceBus's native saga runtime after MyServiceBus defines portable saga and state-machine semantics.

Feature area: orchestration, saga declaration, compiler-generated registration, topology, persistence, and monitoring.

Parent feature proposal: [Sagas and State Machines](sagas-and-state-machines.md).

## Summary

MyServiceBus should explore a Raven-native saga and state-machine DSL as part of a future sample. The DSL would provide a concise declaration of states, handled messages, activities, and transitions while using MyServiceBus's own saga runtime, wire contracts, persistence behavior, topology, and monitoring model.

The language-neutral saga model remains authoritative. Raven syntax must be an idiomatic projection over that model rather than the source from which C# or Java semantics are derived. C# and Java may expose different APIs while sharing the same concepts, observable outcomes, and conformance fixtures.

This macro is not the general MyServiceBus state-machine DSL. The complete feature supplies low-level runtime APIs plus independent, library-based C# and Java DSLs over their native MyServiceBus runtimes, including a non-generated registration path. The Raven macro is an optional higher-level compiler frontend over the same .NET definition and execution model.

The portable model and every language projection should preserve the recognizable primitives and fundamental behavior of MassTransit saga state machines where they remain a sound cross-language fit. MyServiceBus should study MassTransit's implementation and use its proven architecture and behavioral model as the baseline for a MyServiceBus-owned reimplementation in C# and Java. It should not introduce an unrelated state-machine design merely for novelty. An experienced MassTransit user should not have to relearn what a state, event, initial behavior, state-specific behavior, transition, schedule, request, composite event, or finalization means. Exact API copying is not the goal: the low-level runtime, native DSLs, and Raven projection may use different interfaces and syntax while lowering to equivalent behavior.

An initial syntax exploration may look like:

```raven
saga! OrderSaga {
    state AwaitingPayment
    state Processing

    initially {
        on OrderSubmitted {
            publish ReserveInventory(...)
            transition AwaitingPayment
        }
    }

    in AwaitingPayment {
        on PaymentReceived {
            transition Processing
        }
    }
}
```

This sketch communicates the desired source experience; it does not yet settle whether `saga!`, state declarations, message patterns, activities, or transitions should use these exact forms. In particular, the portable semantics must distinguish a targeted `send` from a fan-out `publish`; an inventory-reservation request would ordinarily be modeled as a command sent to an owner rather than an event published to observers.

### Fuller proposed syntax

The fuller proposed macro DSL should be recorded as follows:

```raven
saga! OrderSaga {
    data {
        OrderId: Guid
        PaymentReceived: bool
        InventoryReserved: bool
    }

    correlate OrderSubmitted by OrderId
    correlate PaymentReceived by OrderId
    correlate InventoryReserved by OrderId

    state AwaitingCompletion
    final Completed
    final Cancelled

    initially {
        on OrderSubmitted message {
            set OrderId = message.OrderId
            send ReserveInventory(message.OrderId)
            transition AwaitingCompletion
        }
    }

    in AwaitingCompletion {
        on PaymentReceived {
            set PaymentReceived = true
            transition Completed when InventoryReserved
        }

        on InventoryReserved {
            set InventoryReserved = true
            transition Completed when PaymentReceived
        }

        after 30 minutes {
            publish OrderTimedOut(OrderId)
            transition Cancelled
        }
    }
}
```

This example adds durable saga data, correlation declarations, multiple final states, guarded transitions that accept either event order, a targeted command, and a durable timeout. It is the primary syntax sketch for the future Raven sample. The smaller example above remains useful as the minimal form.

The `correlate ... by OrderId` form still requires a precise rule for whether `OrderId` names a message member, a saga-data member, or a convention matching both. Likewise, `after 30 minutes` must lower to the portable durable-schedule and cancellation model rather than an in-process timer. Those questions should be resolved by the shared saga specification before the macro syntax stabilizes.

## Developer Experience

The macro should be explored only after the saga concepts, runtime stages, persistence boundary, and native C# and Java DSLs are working. At that point, Raven can offer a substantially clearer authoring experience without being asked to define unresolved runtime semantics.

The intended benefit is that a developer can read the workflow in domain order: durable data, correlation, states, initiating events, state-specific reactions, outgoing work, guards, timeouts, and completion. Infrastructure mechanics remain visible where they affect meaning, but fluent-builder ceremony, generic type plumbing, registration boilerplate, and generated adapter details do not dominate the declaration.

This can make state machines easier to:

- learn without first mastering a large .NET or Java fluent API;
- review as one explicit workflow rather than several callbacks and configuration objects;
- compare with process diagrams and business requirements;
- analyze for unreachable states, incomplete correlation, ambiguous transitions, and missing terminal paths; and
- use in documentation and executable samples without maintaining a second pseudocode representation.

The native C# and Java DSLs remain complete alternatives and the primary application-authoring APIs. Lower-level APIs remain available for integrations and generated definitions. Applications should be able to choose the host-language DSL or the Raven macro based on team preference, deployment constraints, and desired compiler diagnostics while receiving the same MyServiceBus runtime behavior.

## MassTransit-Familiar Primitives

The first specification pass should begin from the established MassTransit state-machine building blocks and either preserve them or record a deliberate, justified divergence. Raven may make those concepts feel native through blocks and keywords instead of reproducing the C# fluent API.

| Portable concept | MassTransit-familiar expression | Possible Raven projection |
| --- | --- | --- |
| Saga instance and durable data | `SagaStateMachineInstance` | generated or declared `data` associated with `saga!` |
| Named workflow state | `State`, including `Initial` and `Final` | `state`, `initially`, and `final` declarations |
| Consumed message | `Event<T>` | `on MessageType` |
| Instance creation behavior | `Initially(When(...))` | `initially { on ... }` |
| State-specific behavior | `During(state, When(...))` | `in State { on ... }` |
| Behavior activity | `Then`, `Activity`, `Publish`, `Send`, `Respond` | statements inside an `on` block |
| State change | `TransitionTo` | `transition State` |
| Completion | `Finalize`, `SetCompletedWhenFinalized` | transition to a declared final state plus an explicit retention policy |
| Ignored message | `Ignore` | an explicit `ignore MessageType` declaration |
| Correlation and creation | `CorrelateById`, `CorrelateBy`, `SelectId` | typed `correlate` and instance-ID declarations |
| Durable delayed event | `Schedule`, `Unschedule` | scheduled-event or `after` declarations with cancellation |
| Request lifecycle | state-machine `Request` and its pending/completed/faulted/timeout events | a typed request declaration projected into explicit states and events |
| Join of several events | `CompositeEvent` | a composite declaration or statically checked condition over received-event state |
| Exception behavior | `Catch` and fault activities | typed fault or recovery blocks |

The mapping is semantic rather than necessarily textual. For example, Raven's `in AwaitingPayment` can lower to the MyServiceBus equivalent of MassTransit's `During(AwaitingPayment, ...)`, and `transition Processing` can lower to the equivalent of `TransitionTo(Processing)`, without requiring either spelling in the portable contract. Conversely, a concise Raven feature must not silently weaken correlation, persistence, retry, outbox, or completion behavior established by the MassTransit baseline.

The ordinary C# DSL is expected to begin from the Automatonymous-style composition model, preserving substantial familiarity for common state-machine definitions while allowing deliberate MyServiceBus improvements. Raven is a higher-level projection over that same executable model, not the reason the C# DSL exists and not a separate runtime. The Java DSL maps the same states, events, behaviors, and ordered activities through idiomatic JVM APIs.

The initial scope should favor the common state-machine path. Consumer sagas, routing slips, and every advanced Automatonymous expression are separate compatibility decisions and should not be folded into the first DSL merely because MassTransit supports them.

## MassTransit-Based Reimplementation

MyServiceBus should implement and own its saga runtime in both C# and Java, using MassTransit as the primary design and behavior reference. This is a reimplementation, not a runtime adapter and not a package dependency on MassTransit.

The implementation work should study how MassTransit composes its saga pipeline and translate the relevant design into MyServiceBus's existing abstractions:

- correlate an incoming message before loading or creating an instance;
- select state-specific behavior from the current persisted state;
- execute ordered activities for mutation, send, publish, response, request, scheduling, and transition;
- persist inserts, updates, finalization, and concurrency versions through a repository boundary;
- buffer outgoing operations through the outbox so a persistence conflict does not publish the same work prematurely;
- handle missing instances, ignored events, faults, retries, and out-of-order delivery predictably;
- project state-machine declarations into endpoint topology and runtime monitoring; and
- keep saga support optional for applications that only need the core messaging runtime.

The result should feel structurally familiar in C#, translate naturally into Java, and remain implemented entirely through MyServiceBus-owned contracts and runtime components. The reference implementations may organize classes and execution stages differently where their platforms require it, but they must share the same transition and failure semantics through canonical fixtures.

MassTransit's source can inform implementation choices, test cases, edge conditions, and terminology. Any source incorporated directly must retain all notices and comply with its applicable license; ordinary MyServiceBus packages must continue to publish an accurate dependency and licensing boundary. The default goal is behavioral and architectural reimplementation rather than embedding MassTransit assemblies.

## Architectural Boundary

The Raven compiler or a Raven-owned MyServiceBus integration should lower a saga declaration into ordinary runtime artifacts:

- a saga-data type and stable saga type identity;
- explicit correlation descriptors for every initiating and subsequent message;
- a statically generated MyServiceBus state and transition table;
- ordinary send, publish, schedule, and consume operations;
- normalized saga, state-machine, message, endpoint, and persistence-requirement topology;
- registration metadata that does not require runtime source inspection; and
- monitoring metadata for state distribution, transitions, faults, timeouts, and correlations.

Raven syntax trees, compiler callbacks, executable delegates, and compiler-private layouts must not enter persisted saga records, message envelopes, or portable topology snapshots. The generated path should be suitable for trimming and NativeAOT and should not require Raven- or MassTransit-specific behavior in transports, serializers, retry pipelines, or settlement code.

Raven remains a separate product. MyServiceBus can host an executable interoperability sample and consume stable Raven-produced CLR artifacts without making the Raven compiler part of the core build or requiring other clients to understand Raven declarations.

## Semantics Required Before the DSL

The shared saga specification and conformance suite must define these behaviors before the sample fixes a convenient syntax around them:

- saga identity, instance creation, and message correlation;
- durable state representation and versioning;
- missing-instance and duplicate-message behavior;
- idempotency, optimistic concurrency, conflict retries, and ordering expectations;
- atomic persistence of state changes and outgoing messages through the outbox boundary;
- initial, intermediate, final, and completed-instance behavior;
- guards, conditional transitions, ignored messages, and unhandled messages;
- activities, faults, retry effects, compensation, and recovery;
- durable timeouts, scheduling, cancellation, and late-message behavior;
- concurrent or composite conditions, such as waiting for both payment and inventory;
- topology identity and persistence-provider requirements; and
- application-oriented monitoring without exposing saga payloads by default.

The portable specification describes concepts and observable behavior. It must not require a particular fluent builder, annotation, language keyword, or generated CLR representation.

For each primitive, the specification work should compare MyServiceBus behavior with the supported MassTransit interoperability baseline, identify what is observable across the wire and repository boundary, and add cross-language fixtures for the behavior MyServiceBus claims. Familiar naming without equivalent failure, correlation, persistence, and completion behavior is not sufficient compatibility.

## Compile-Time Value

The DSL is valuable only if it offers more than shorter registration syntax. A Raven implementation should explore compile-time diagnostics for:

- states that cannot be reached;
- non-final states with no outgoing behavior;
- duplicate or ambiguous handlers for the same message and state;
- initiating or correlated messages without a correlation rule;
- transitions to undeclared states;
- final states that declare later transitions;
- potentially conflicting unconditional transitions; and
- declared message activities that cannot be represented in portable topology.

The compiler should emit a deterministic descriptor manifest or equivalent generated registration artifact. That artifact should project to the same normalized model and pass the same behavioral fixtures as C# and Java implementations.

## Future Executable Sample

After the native MyServiceBus saga runtime and portable saga profile exist, add a sample under `test/Experiments` or the repository's future samples area that proves the DSL against packaged MyServiceBus artifacts. The sample should:

1. define a small order saga in Raven;
2. correlate an initiating message and at least two subsequent messages;
3. persist and reload saga state through a supported provider;
4. exercise a durable timeout and one concurrency conflict;
5. send or publish an outgoing contract consumed by a C# or Java peer;
6. expose the same normalized topology and monitoring facts as an equivalent C# declaration and Java saga;
7. prove that the Raven declaration executes through the same MyServiceBus saga runtime used by ordinary C# declarations rather than a Raven-only state-machine runtime; and
8. run without reflection-only knowledge of the Raven source form.

The sample should exercise at least the familiar `Initially`/`During`/`When` model, correlation, transition, finalization, one scheduled event, and one request or composite event. Its documentation should show the equivalent MassTransit concepts and the corresponding C# and Java MyServiceBus projections without requiring identical syntax.

The sample is evidence for Raven's projection and cross-language interoperability. It must not become the sole proof of portable saga behavior.

### Current prototype boundary

The first executable prototype now lowers a Raven declaration macro into the native .NET `SagaStateMachine<TSaga>` authoring API and executes the resulting definition through `SagaStateMachineRuntime<TSaga>`. It covers generated data, identity correlation, initial and state-specific behaviors, mutation, send, publish, transition, and finalization. It intentionally retains finalized instances so the experiment can inspect the runtime result.

The prototype does not implement named final outcomes, guarded transitions, durable `after` scheduling, requests, or composite events. Those constructs remain in the fuller syntax proposal above, but the macro diagnoses them where applicable rather than assigning semantics that the portable version 1 runtime does not yet define. Its convention-based `send` destination is sample-only and must be replaced by an explicit or topology-resolved destination model before this becomes an authoring feature.

The prototype also establishes a tooling boundary. Saga-owned positions complete from the declaration itself: correlated events after `on`, declared states after `in` and `transition`, saga-data members after `set` and `by`, and the supported activity vocabulary inside a behavior. The right-hand side of `set` and the outgoing constructor after `send` or `publish` are Raven expression fragments with a typed handler alias. Raven therefore owns expression parsing and ordinary language services only where the saga grammar explicitly admits an expression; it does not turn the behavior body into unrestricted host-language code.

That distinction is part of the proposed language contract. A future implementation should model each position with an explicit expected category—state, event, data member, duration, destination, activity, type, or Raven expression—and produce recovery-aware diagnostics and completion from that category. Adding an escape hatch must not make invalid saga concepts appear valid merely because they form a valid Raven expression.

## Relationship to Choreography

Choreography support remains decentralized and does not use this DSL. Its declarations and diagnostics may help identify real event relationships before saga semantics are finalized, but a Raven `saga!` declaration represents orchestration: one coordinator owns durable workflow state and directs subsequent work.

## Open Questions

1. Should the DSL be implemented by the Raven compiler, a compiler plugin, or generated library declarations?
2. Which generated descriptor ABI can be shared with other future .NET language projections?
3. How should saga data be declared without conflating persisted application data with control state?
4. Should message handlers allow unrestricted Raven code, or should transition-producing constructs be constrained for stronger static analysis?
5. How should guards and composite events remain deterministic under optimistic-concurrency retries?
6. Which parts of the declaration should appear as provenance in inspection and monitoring without making Raven syntax portable metadata?
7. Which MassTransit primitives belong in the first portable profile, and which require explicit later capability profiles?
8. Which MassTransit implementation stages should MyServiceBus preserve directly, and which should be reshaped around the existing portable pipeline?
9. Which behavior differences between the MassTransit 8.5.1 interoperability baseline and later releases are relevant to the MyServiceBus saga profile?

## References

- [MassTransit saga state machines](https://masstransit.io/documentation/patterns/saga/state-machine)
- [MassTransit 8.5.1 source](https://github.com/MassTransit/MassTransit/tree/v8.5.1)
- [Sagas and State Machines Proposal](sagas-and-state-machines.md)
- [MyServiceBus Design Goals](../development/design-goals.md)
- [MyServiceBus Architecture](../myservicebus-architecture.md)
