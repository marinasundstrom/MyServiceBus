# Choreography Modeling and Diagnostics Proposal

## Status

Future product-area proposal with its first foundation slices implemented. Message-operation observations carry message identity and consume-to-outbound causal identity in C# and Java. The collector prefers exact envelope matches for deliveries, reports correlation fallback explicitly, and exposes exact local reactions separately. Matching C# and Java builders produce the same versioned, deterministic choreography fragment from a shared fixture. Applications can register validated fragments, and topology version 2 plus inspection and monitoring metadata expose them without changing delivery. The monitoring service merges cross-application fragments, collapses identical replica declarations, retains freshness, and reports definition conflicts through a read-only query. The Dashboard now renders this declared definition map grouped by application before attempting observed deviations. Declared-versus-observed comparison, behavioral diagnostics, and runtime overlays remain future work. This document is not a supported lifecycle API, alerting service, or workflow runtime commitment.

## Summary

MyServiceBus should make application-composed choreography easier to understand and operate without turning it into orchestration. Services continue to react independently to messages through the existing send, publish, and consume primitives. MyServiceBus adds portable declarations, more precise causal observations, graph comparison, and bounded diagnostics around those reactions.

The first feature should answer three different questions without conflating them:

1. **Configured routing:** where can a message travel according to endpoint and broker topology?
2. **Declared choreography:** which reactions does an application say it may or should perform?
3. **Observed flow:** which deliveries and reactions were actually observed within a bounded, explicitly incomplete time window?

A later discovery view may ask a fourth question: **which recurring coordination patterns appear workflow-shaped even though nobody formally declared a workflow?** This is a derived interpretation of repeated observed flow, not a new source of runtime truth. It can help a developer recognize an implicit workflow that has emerged from contracts, producers, and consumers spread across services.

This work should precede the saga runtime because it builds on capabilities that exist today, improves operations without owning business state, and establishes causation, completeness, and flow-monitoring semantics that saga monitoring can later reuse.

## Priority Decision

Choreography is the first workflow-coordination experience to design and deliver. The initial product path is declaration, validation, causal observation, graph comparison, diagnostics, and a focused Dashboard view. It does not begin by implementing a saga repository or a centralized workflow runtime. Orchestration should reuse the evidence model and visualization language after this decentralized experience is coherent.

## Product Boundary

Choreography is decentralized workflow coordination and has no central coordinator. Participants react to messages according to their own local rules and collectively produce the broader process. Each service owns its consumers, business decisions, and outgoing messages. A declaration describes those independently owned reactions; it does not move their execution into a monitoring service or create shared workflow state.

Applications can implement these reactions directly with the existing publish, send, and consume APIs. The proposed feature adds a clearer portable way to express that intent and diagnose its observed behavior in C# and Java; it does not claim ownership of the underlying business workflow.

The first feature should include:

- portable declarations of trigger-to-output relationships;
- exact producer-to-consumer matching when message identity is available;
- explicit causal links between a consumed message and outgoing operations created while handling it;
- configured, declared, and observed graph projections kept as separate datasets;
- bounded diagnostics for missing routes, undeclared observations, cycles, and unexpected amplification;
- opt-in timing and multiplicity expectations;
- explicit confidence, freshness, coverage, and dropped-observation reporting;
- matching C# and Java descriptor APIs and canonical fixtures;
- monitoring-service query models and application-oriented dashboard views; and
- one cross-language executable sample.

The first feature should not include:

- a coordinator, saga repository, or framework-owned business-process instance;
- automatic compensation or commands that mutate application state;
- inference of business success or failure from message traffic alone;
- payload capture or arbitrary-header export;
- unbounded storage keyed by every correlation or conversation identity;
- source-code analysis that guesses every possible outgoing message; or
- an alerting lifecycle with acknowledgement, escalation, and notification policy.

## Protocol Compatibility and Innovation Boundary

Choreography modeling sits above the transport protocol. A MyServiceBus service can send, publish, consume, respond, and fault using the verified MassTransit-compatible contracts, envelopes, addresses, headers, and broker topology while describing those reactions through MyServiceBus-owned APIs and operational metadata.

MassTransit peers do not need to understand a choreography definition to participate in its message flow. Their observed sends and consumptions can appear in the graph when monitoring or interoperable telemetry supplies sufficient identity, but declaration fragments and diagnostics remain an optional MyServiceBus operations-plane capability rather than new application message contracts.

This separation leaves room to innovate in declaration syntax, causal analysis, graph comparison, cycle and amplification diagnostics, and dashboard experience without changing the underlying wire profile. A compatibility claim must remain precise: protocol-compatible messages can participate in the same choreography, but MassTransit does not automatically consume MyServiceBus declaration metadata or produce MyServiceBus completeness evidence.

The choreography definition itself remains local to the service that owns each reaction. It is not exchanged as part of the application protocol and another service does not invoke it remotely. The normalized fragment exists so MyServiceBus can validate equivalent concepts in C# and Java and project local intent into topology and monitoring. The actual inter-service interface remains the produced and consumed message contracts plus their delivery protocol.

That is more than a cosmetic distinction when failures occur. A local reaction's retry, idempotency, outbox, and acknowledgement behavior determines whether other services observe zero, one, or duplicate outputs. Those behaviors must therefore be specified and tested even though the reaction code and declaration never cross the service boundary.

Protocol compatibility is an acceptance gate for every choreography slice:

- declarations and workflow identities remain local topology or monitoring metadata;
- the feature does not require a new application message, envelope field, header, exchange, queue, or address convention;
- ordinary sends, publications, responses, faults, retries, and settlements continue to follow the selected MassTransit-compatible transport profile;
- monitoring-only causal evidence is captured beside delivery and is never required by a peer to deserialize, route, consume, or acknowledge a message;
- C# and Java conformance fixtures verify the same normalized declarations and diagnostics; and
- mixed MyServiceBus and MassTransit broker tests verify that enabling choreography support does not change the interoperable wire envelope or routing behavior.

A later, explicitly versioned protocol proposal would be required before any choreography concept could cross the service boundary as new wire metadata. It must not arrive accidentally through an implementation detail of the Dashboard or declaration DSL.

## Existing Foundation and Current Limitation

The monitoring service already exposes configured topology and bounded observed-flow graphs at application and replica resolution. C# and Java message-operation observations include envelope message ID. Flow reconstruction first matches an outbound operation to consumption by that exact identity and labels the edge `exact_message`; older or incomplete observations fall back to correlation ID, conversation ID, or trace ID and are labeled `correlated`.

Consumer-originated sends, publications, responses, and faults also record the consumed envelope as `causationMessageId`. That monitoring-only identity survives outbox capture and PostgreSQL dispatch, and the collector exposes exact trigger-to-output reactions through the causal-flow query with `exact_causation` confidence.

The aggregate producer-to-consumer graph remains distinct from exact local reaction causality:

- they do not contain the envelope initiator ID;
- older clients and operations created outside a consume context do not contain causal identity;
- a correlation or conversation can contain several concurrent messages and branches;
- correlation fallback retains one source for a shared key, so another outbound observation may replace it; and
- dropped observations, offline exporters, retention bounds, and services without monitoring can remove part of the path.

The existing graph must therefore remain labeled **observed aggregate flow**. Choreography work should strengthen it rather than retroactively describing heuristic correlation as exact causation.

## Three Graphs

### Configured routing graph

The configured graph continues to project broker and endpoint possibilities from normalized topology. It can show that a published contract has subscribers or that a directed-send destination exists. It cannot prove that application code emits a message or that a consumer will choose a particular reaction.

### Declared choreography graph

Applications may publish versioned, payload-free fragments describing their owned reactions. A fragment should contain:

| Field | Meaning |
| --- | --- |
| choreography ID | Stable logical flow identity shared by independently owned fragments |
| definition version | Version of the declaration contract, not an instance counter |
| step ID | Stable identity for one application-owned reaction |
| owner | Application and optional component or consumer identity |
| trigger | Consumed message contract or explicitly external starting condition |
| output | Sent or published contract, response, scheduled contract, or explicit no-output terminal reaction |
| operation kind | `send`, `publish`, `respond`, `schedule`, or another portable operation |
| requirement | Informational, optional, or expected when its declared condition applies |
| multiplicity | Optional minimum and maximum expected output count |
| time expectation | Optional bounded duration after the trigger observation |
| correlation mode | Identity expected to connect observations, without serializing an expression |

Declarations are metadata. They do not execute reactions, evaluate business predicates, or guarantee that the declared output occurs. A conditional reaction may be declared as optional or may use an application-owned condition identity; the monitoring service does not receive or run the condition.

Fragments merge by stable identities without requiring one service to own the entire choreography definition. The first read model collapses identical replica declarations and reports definition-version, owner, and step-ownership disagreement while preserving online and reporting replica counts and the latest capture time. These are configuration conflicts, not evidence that a business workflow failed. Incompatible contract identity and contradictory expectation analysis remain later comparison work.

### Observed flow graph

Observed flow is built from immutable, payload-free operation evidence within a bounded time range. It should expose the strength of each relationship:

| Confidence | Evidence |
| --- | --- |
| Exact delivery | An outbound and consumed observation share the same message ID |
| Exact local cause | An outgoing operation records the consumed message ID or operation ID that directly caused it |
| Trace-derived cause | Producer and consumer spans have an observed parent relationship |
| Correlated | Only correlation, conversation, initiator, or trace identity connects the records |
| Aggregate only | Counts align by application, contract, endpoint, and window without an instance-level link |

Queries and dashboards must retain this confidence instead of flattening every edge into the same visual claim.

### Discovered coordination patterns

Repeated bounded causal chains may reveal a stable sequence, branch, cycle, or participant set that looks like an implicit workflow. The monitoring service may eventually group that evidence into a **discovered coordination pattern** and show its supporting sample count, observation window, first and last occurrence, participating applications and contracts, edge confidence, and coverage. A useful first implementation can be statistical and deterministic; it does not require opaque machine-learning classification.

The label must remain epistemically modest. Recurrence does not prove shared business intent, define a beginning or terminal outcome, or create a workflow instance. The dashboard should say **candidate pattern** or **recurring message pattern**, not silently promote the result to a choreography definition. A developer may use the pattern as the starting point for an explicit declaration, but generated declarations remain drafts until an application owner reviews and registers them.

The inverse view is also useful. Once a pattern has enough bounded support, the dashboard can highlight a causal edge or participant that breaks out of it: a new contract, unexpected consumer, rare branch, changed cycle, missing usual continuation, or materially different fan-out. Such a breakout is a **deviation from the selected baseline**, not automatically a defect. Deployments, version skew, feature flags, low-frequency valid paths, incomplete telemetry, and changing traffic mix can all explain it. The view must therefore expose the baseline window and sample size, compare like application and definition versions where possible, retain observation confidence, and allow the user to inspect the underlying causal chains.

## Observation Contract Extension

A monitoring protocol extension should add the minimum identifiers required for exact reconstruction. The first item is implemented additively; the rest remain future work:

- `messageId` for the envelope involved in the operation (**implemented**);
- `initiatorId` with its existing envelope meaning;
- `causationMessageId` for the consumed envelope that directly caused an outgoing operation (**implemented**, including outbox persistence);
- `operationId` for one local send, publish, consume, response, or schedule operation;
- `causationOperationId` where an operation directly creates another local operation;
- `parentSpanId` when trace context provides it; and
- stable consumer or handler identity when the normalized topology can supply it.

`causationMessageId` is monitoring evidence and does not need to become a new wire header. A consume context can attach the current inbound message identity when it emits an outbound hook. Outbox capture must preserve that observation relationship until actual dispatch without reporting capture as broker delivery.

Identifiers remain payload-free operational metadata. Exporters still batch through bounded queues, and dropping an observation must reduce reported completeness rather than block or fail message delivery.

## Native Declaration APIs

C# and Java provide corresponding descriptor and builder APIs that produce the same normalized fragment. The fragment is registered explicitly with `AddChoreography` or `addChoreography`; registration validates it and includes it in normalized topology, inspection, and monitoring metadata.

The C# shape is:

```csharp
ChoreographyFragment fragment = new ChoreographyBuilder(
        "order-fulfillment",
        definitionVersion: "1",
        owner: "orders")
    .Step<OrderSubmitted>("reserve-inventory", step => step
        .OwnedBy<SubmitOrderConsumer>()
        .Sends<ReserveInventory>("queue:reserve-inventory", expect => expect
            .Within(TimeSpan.FromSeconds(5))
            .Exactly(1)))
    .Step<InventoryReserved>("request-payment", step => step
        .Publishes<PaymentRequested>())
    .Build();

configurator.AddChoreography(fragment);
```

The Java shape is:

```java
ChoreographyFragment fragment = new ChoreographyBuilder(
        "order-fulfillment",
        "1",
        "orders")
    .step("reserve-inventory", OrderSubmitted.class, step -> step
        .ownedBy(SubmitOrderConsumer.class)
        .sends(ReserveInventory.class, "queue:reserve-inventory", expect -> expect
            .within(Duration.ofSeconds(5))
            .exactly(1)))
    .step("request-payment", InventoryReserved.class, step -> step
        .publishes(PaymentRequested.class))
    .build();

configurator.addChoreography(fragment);
```

The normalized schema records the choreography and definition identities, owning application, stable step identity, trigger message URN, optional owning component, and ordered outputs. Outputs support `send`, `publish`, `respond`, `schedule`, and an explicit terminal outcome plus informational, optional, or expected requirements, bounded multiplicity, and a millisecond timing expectation. Explicit URN overloads support canonical cross-language definitions when language type names differ.

A declaration attached directly to consumer registration may ultimately be more discoverable than a separate global builder. Source generators and annotation processors may emit fragments when declarations are explicit in source, but they must not attempt whole-program analysis or become the only registration path.

### Future Raven projection

Raven may later provide a concise choreography DSL over this same declaration model. That projection could make trigger-to-output relationships, optional branches, timing expectations, and terminal reactions read more like a workflow description while lowering into an ordinary `ChoreographyFragment` and the same registration and monitoring path.

The Raven DSL must remain optional. It does not define the portable semantics, replace the complete C# and Java builders, introduce executable coordination, or add Raven-specific message headers or broker behavior. Raven syntax and compiler artifacts remain outside normalized fragment identity, persisted monitoring data, and the MassTransit-compatible wire protocol.

## Diagnostics

Diagnostics should compare the three graphs and state the evidence behind every result.

### Configuration diagnostics

- A declared send has no configured destination.
- A declared publication has no configured subscriber where the deployment expects one.
- An observed contract or route is absent from the application's declaration.
- Independently published fragments disagree on version, ownership, or contract identity.
- A declaration contains unreachable steps, structural cycles, or a terminal reaction with outgoing expectations.

### Runtime diagnostics

- An exact outbound delivery has no observed consumption within the selected window.
- An expected reaction has no matching output within its declared time expectation.
- Observed output multiplicity exceeds a declared maximum.
- A conversation repeatedly traverses a declared cycle beyond an explicit bound.
- Fan-out or repeated reaction grows materially beyond its declared or learned baseline.
- A message is consumed by an application or handler that the declared graph did not identify.
- A sufficiently supported recurring pattern gains a new participant, contract, branch, cycle, or fan-out shape relative to its selected baseline.

Terms such as **unobserved**, **overdue observation**, or **expectation not evidenced** are preferable to **workflow failed**. A diagnostic may become actionable only when its declaration is explicit and the relevant observation window is complete enough to support the claim.

## Completeness and Confidence Rules

Every diagnostic query should report:

- requested and effective time window;
- exporter freshness for participating applications;
- known online and offline instances;
- dropped observation counts;
- monitoring-storage gaps and retention coverage;
- applications present in topology but absent from monitoring;
- proportion of edges supported by exact, trace-derived, correlated, or aggregate evidence; and
- whether the declaration set is complete for the selected choreography version.

Hard diagnostic severity must be suppressed or downgraded when required evidence is incomplete. For example, a missing expected output cannot be called overdue when the producing application's exporter was offline during the expectation window. A structural cycle in declarations remains valid even when runtime observations are incomplete because it is a statement about configuration, not execution.

## Bounded State and Privacy

The collector should retain bounded observations and aggregate diagnostics. It should not create a permanent workflow instance for every correlation identity. Short-lived causal chains may be assembled within the active window and discarded or compacted according to monitoring retention.

Definitions, observations, and diagnostics exclude message bodies and arbitrary headers. Correlation and message identities may still be sensitive operational identifiers, so instance-level drill-down requires the same authentication, authorization, retention, and redaction review as failed-message inspection. Aggregate graphs remain the default dashboard view.

## Topology and Monitoring Integration

The portable topology model should add typed declared-reaction relationships without changing message, endpoint, consumer, or binding identity. A reaction links an owning consumer or component, one trigger contract, one output contract, and an operation kind. Expectations and choreography membership are additive metadata.

The monitoring service should expose separate query models for:

- configured routing;
- declared choreography;
- observed application and replica flow;
- bounded causal chains where exact evidence exists;
- recurring coordination-pattern candidates and deviations from an explicit bounded baseline;
- graph differences; and
- diagnostics with confidence and completeness.

The dashboard should visually distinguish possible, declared, exact-observed, heuristic-observed, and pattern-derived edges. A discovered pattern should have a different visual treatment from a declared workflow, while breakouts should appear as overlays against the selected baseline rather than rewriting it. Cycles and amplification should appear in a focused choreography view before any concise signal is promoted to the overview.

This becomes one half of a shared workflow-observation experience. For choreography, the dashboard reconstructs a bounded story from independently owned declarations and message evidence: which participants reacted, where the flow branched, which expected reaction has not yet been evidenced, and how confident the reconstruction is. It must not invent an authoritative workflow instance, current state, or terminal business outcome when no participant owns one.

Orchestration can use the same visual language while supplying stronger evidence. A saga runtime owns an instance identity and persisted state, so its view can show the authoritative current state, completed transitions, pending timeouts or requests, emitted messages, conflicts, faults, and completion. Operators should be able to move from an aggregate workflow map to one bounded causal chain or saga instance and back without confusing inferred choreography progress with persisted orchestration state.

The choreography definition map should group reaction steps inside their owning application boundaries and connect consumed trigger contracts to sent, published, scheduled, responded, or terminal outcomes. Its runtime overlay can encode observed volume, latency, amplification, and confidence on those edges. Selecting a causal chain opens an ordered message-reaction timeline with application, component, operation kind, safe contract identity, message and correlation references, timestamp, duration, retry or fault category, and declared-step match. Undeclared edges and missing expected observations are overlays, not rewritten workflow structure.

## Cross-Language Fixtures

Canonical fixtures should prove that C# and Java serialize equivalent fragments and diagnostics for:

- one trigger with one directed command;
- publication fan-out to several applications;
- an optional conditional output;
- an explicit terminal reaction;
- a bounded structural cycle;
- declared and observed multiplicity;
- exact message delivery across C# and Java;
- causation from consume to send or publish;
- correlation-only fallback;
- missing observations and dropped batches;
- conflicting definition versions; and
- canonical ordering and stable identities.

The first executable choreography sample reuses the mixed C# and Java monitoring environment. Both applications register separately owned reactions under one `sample-order-submission` definition, matching their real `SubmitOrder` to `OrderSubmitted` publication and terminal observation behavior. The initial sample proves cross-language declaration merging and the Dashboard definition map. Later diagnostic slices should extend it with deliberately unhealthy paths for missing routing, a bounded timeout expectation, and unexpected amplification. A dedicated Aspire workflow application becomes more valuable with the orchestration runtime, when it can demonstrate persisted saga state, timeouts, recovery, and Dashboard state-machine views. That later environment should also include an event leaving the orchestrator boundary for a choreographed reaction, proving that both coordination relationships can coexist.

## Recommended Delivery Sequence

1. Specify configured, declared, observed, and causal relationships plus their confidence levels.
2. Add canonical declaration fixtures and matching C# and Java builders (**implemented first slice**); add diagnostic fixtures before choosing their final APIs.
3. Extend the monitoring observation contract with message, initiator, causation, operation, and parent-span identities; message and causal identity are implemented.
4. Extend exact producer-to-consumer matching into explicit consume-to-outbound causation (**implemented first slice**).
5. Attach the implemented declaration builders to registration and add normalized topology relationships (**implemented first slice**).
6. Implement graph comparison and diagnostics in the monitoring service with completeness gates.
7. Add the focused declared-workflow dashboard view (**implemented first view**) and cross-language sample.
8. Explore bounded recurring-pattern discovery and breakout visualization only after exact causal chains and comparison evidence are trustworthy.
9. Evaluate demand for an explicit per-conversation lifecycle model only after the bounded diagnostic model has production evidence.

## Relationship to Sagas

The [Sagas and State Machines Proposal](sagas-and-state-machines.md) defines orchestration separately. Saga monitoring can reuse exact delivery identity, causal relationships, graph confidence, and completeness rules from this work. A saga transition remains stronger evidence because the framework owns and persists coordinator state; choreography diagnostics must not imply the same ownership.

## Open Questions

1. Should choreography fragments be attached to consumers, applications, or a separate logical-flow registration surface?
2. Which expectations can be expressed without sending business predicates or payload data to monitoring?
3. Should exact causal identity remain monitoring-only, or is a portable wire-level causation header eventually justified?
4. How should definition versions from independently deployed services merge during rolling upgrades?
5. Which cycle and amplification thresholds are portable declarations versus dashboard policy?
6. When is an undeclared observed edge a useful discovery signal rather than configuration drift?
7. What retention is sufficient for causal drill-down without creating an unbounded workflow-history product?
8. What recurrence, sample size, version segmentation, and stability thresholds make a candidate pattern useful without overstating intent?

## References

- [Runtime Monitoring](runtime-monitoring.md)
- [Topology Model Specification](../specs/topology-model-spec.md)
- [Topology Extension Model](../specs/topology-extension-model.md)
- [Sagas and State Machines Proposal](sagas-and-state-machines.md)
- [MyServiceBus Architecture](../myservicebus-architecture.md)
