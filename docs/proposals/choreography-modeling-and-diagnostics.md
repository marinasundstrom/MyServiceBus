# Choreography Modeling and Diagnostics Proposal

## Status

Future product-area proposal with its choreography MVP foundation implemented. Message-operation observations carry message identity and consume-to-outbound causal identity in C# and Java. The collector prefers exact envelope matches for deliveries, reports correlation fallback explicitly, and exposes exact local reactions separately. Matching C# and Java builders produce the same versioned, deterministic choreography fragment from a shared fixture. Applications can register validated fragments, and topology version 2 plus inspection and monitoring metadata expose them without changing delivery. The monitoring service merges cross-application fragments, collapses identical replica declarations, retains freshness, reports definition conflicts, and projects version-scoped output-to-trigger connections through a read-only query. Separate bounded projections reconstruct exact workflow runs with steps, handoffs, retries, faults, and completeness-gated comparison of declared send, publish, and terminal outcomes with exact observed evidence. The Dashboard renders the stable declaration map, aggregate evidence, selectable live-updating run flow, and per-step comparison findings. Heuristic correlation, formal joins, pattern discovery, and broader graph diagnostics remain future work. This document is not a supported lifecycle API, alerting service, or workflow runtime commitment.

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

Choreography is decentralized workflow coordination and has no central coordinator. Participants react to messages according to their own local rules and collectively produce the broader process. That workflow knowledge commonly appears inline: a consumer handles one contract, makes a local decision, and sends or publishes the next contract. Each service owns its consumers, business decisions, and outgoing messages. A choreography declaration enriches those independently owned reactions with shared workflow, step, ownership, expectation, and monitoring context; it does not move their execution into a monitoring service or create shared workflow state.

Applications can implement these reactions directly with the existing publish, send, and consume APIs. The proposed feature adds a clearer portable way to express that intent and diagnose its observed behavior in C# and Java; it does not claim ownership of the underlying business workflow.

The normalized fragment is the common semantic and operations model, not a requirement that every language expose the same builder surface. Choreography should have several abstraction levels:

1. portable declaration, causal-evidence, validation, and monitoring contracts;
2. low-level descriptor and registration APIs for generated definitions, integrations, and tooling;
3. idiomatic C# and Java authoring APIs that project local consumer reactions into those descriptors; and
4. optional higher-level projections, including a future Raven macro DSL.

The authoring layers may improve upon the current builder without changing the portable model. MyServiceBus can draw from established event-driven, workflow, statechart, and language-native API designs without inheriting another library's historical overloads, inheritance constraints, or source-compatibility burden. C# may use fluent expressions, attributes, generated declarations, or consumer-attached configuration where those fit naturally; Java may use builders, annotations, processors, or other JVM conventions. Equivalent semantics and normalized output matter more than textual symmetry.

The first feature should include:

- portable declarations of trigger-to-output relationships;
- exact producer-to-consumer matching when message identity is available;
- explicit causal links between a consumed message and outgoing operations created while handling it;
- configured, declared, and observed graph projections kept as separate datasets;
- bounded diagnostics for missing routes, undeclared observations, cycles, and unexpected amplification;
- opt-in timing and multiplicity expectations;
- explicit confidence, freshness, coverage, and dropped-observation reporting;
- matching C# and Java normalized descriptors and canonical fixtures, with idiomatic authoring projections above them;
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

Repeated bounded causal chains may reveal a stable sequence, branch, cycle, or participant set that looks like an implicit workflow. The monitoring service—not the Dashboard—may eventually group that retained evidence into a **discovered coordination pattern** and return its supporting sample count, observation window, first and last occurrence, participating applications and contracts, edge confidence, and coverage. A useful first implementation can be statistical and deterministic; it does not require opaque machine-learning classification.

The label must remain epistemically modest. Recurrence does not prove shared business intent, define a beginning or terminal outcome, or create a workflow instance. The dashboard should say **candidate pattern** or **recurring message pattern**, not silently promote the result to a choreography definition. A developer may use the pattern as the starting point for an explicit declaration, but generated declarations remain drafts until an application owner reviews and registers them.

The inverse view is also useful. Once a pattern has enough bounded support, the monitoring service can project a causal edge or participant that breaks out of it: a new contract, unexpected consumer, rare branch, changed cycle, missing usual continuation, or materially different fan-out. The Dashboard may highlight that result but does not calculate it. Such a breakout is a **deviation from the selected baseline**, not automatically a defect. Deployments, version skew, feature flags, low-frequency valid paths, incomplete telemetry, and changing traffic mix can all explain it. The projection must therefore expose the baseline window and sample size, compare like application and definition versions where possible, retain observation confidence, and let query clients retrieve the underlying causal chains.

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

C# and Java currently provide corresponding descriptor and builder APIs that produce the same normalized fragment. The current convenience API configures the builder through `AddChoreography` or `addChoreography`; overloads also accept an existing builder or a prebuilt fragment for generated definitions, fixtures, reuse, and tooling. Every path validates and includes the same fragment in normalized topology, inspection, and monitoring metadata.

These builders establish the low-level portable declaration boundary; they do not freeze the eventual preferred authoring DSL. Higher-level APIs may attach declarations directly to consumer registration, infer safe local identities from typed code, reduce repeated message and owner declarations, or express branches and expectations more clearly. Such conveniences must remain inspectable, must lower deterministically into `ChoreographyFragment`, and must never infer executable business rules that the application did not declare.

The C# shape is:

```csharp
configurator.AddChoreography(
        "order-fulfillment",
        definitionVersion: "1",
        owner: "orders",
        workflow => workflow
            .Step<OrderSubmitted>("reserve-inventory", step => step
                .OwnedBy<SubmitOrderConsumer>()
                .Sends<ReserveInventory>("queue:reserve-inventory", expect => expect
                    .Within(TimeSpan.FromSeconds(5))
                    .Exactly(1)))
            .Step<InventoryReserved>("request-payment", step => step
                .Publishes<PaymentRequested>()));
```

The Java shape is:

```java
configurator.addChoreography(
        "order-fulfillment",
        "1",
        "orders",
        workflow -> workflow
            .step("reserve-inventory", OrderSubmitted.class, step -> step
                .ownedBy(SubmitOrderConsumer.class)
                .sends(ReserveInventory.class, "queue:reserve-inventory", expect -> expect
                    .within(Duration.ofSeconds(5))
                    .exactly(1)))
            .step("request-payment", InventoryReserved.class, step -> step
                .publishes(PaymentRequested.class)));
```

The normalized schema records the choreography and definition identities, owning application, stable step identity, trigger message URN, optional owning component, and ordered outputs. Outputs support `send`, `publish`, `respond`, `schedule`, and an explicit terminal outcome plus informational, optional, or expected requirements, bounded multiplicity, and a millisecond timing expectation. Explicit URN overloads support canonical cross-language definitions when language type names differ.

A declaration attached directly to consumer registration may ultimately be more discoverable than a separate global builder. Source generators and annotation processors may emit fragments when declarations are explicit in source, but they must not attempt whole-program analysis or become the only registration path. C# and Java projections need equivalent expressive power and validation outcomes, not identical spelling or framework machinery.

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

A request and its response should appear as one paired interaction rather than two independent branches. The activity graph uses a solid outbound request edge and a dashed return response edge sharing one request identity; the caller activity can show its waiting phase while the responder remains a separate activity. Selecting the pair opens a compact sequence-style detail that separates request handoff, responder execution, response handoff, and total round-trip time. Fault and timeout outcomes return on the same paired path with distinct treatment. Exact response comparison remains unsupported until monitoring records the portable request, response, and timeout identities needed to establish that pairing.

The selected-chain experience should be presented as a bounded **workflow run** in the Dashboard. A D3 activity diagram uses application swimlanes, consumer or reaction activities, and causal message edges to show the ordered and branched path; an adjacent step breakdown keeps the precise evidence inspectable and accessible. Each step maps a consumed message to its application, endpoint, and declared consumer or component where possible, then shows handler duration, success or failure, retries, and causally emitted messages. Connections show outgoing-operation and observed handoff timing separately from consumer execution time. The implemented exact-evidence assembler now groups same-message delivery fan-out and weakly connected causal components, reconciles retained partial roots when a later observation connects them, and reports linear, branching, converging, or combined graph shape. The D3 view labels root fan-out, forks, and convergence, but convergence remains observed graph structure rather than a declared join. Formal fork/join and decision/merge notation can be added as the declaration model gains the necessary semantics. Faulted consumption, exhausted retry, failed outbound operation, and uncertain or absent continuation must remain distinct conditions rather than becoming one generic failed-workflow badge.

This run view is an early choreography MVP priority because it answers the developer's immediate debugging questions: what happened, which participant handled each step, how long each part took, which message advanced the flow, and where failure evidence appeared. It remains a reconstructed operational view, not a business workflow-instance repository. The monitoring service now owns chain assembly, evidence classification, bounded retention, filtered pagination, and direct run lookup; the Dashboard owns the graph, timeline, selection, density, and drill-down interaction.

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

The executable choreography samples reuse the mixed C# and Java monitoring environment. Both applications register separately owned reactions under one `sample-order-submission` definition, matching their real `SubmitOrder` to `OrderSubmitted` behavior and proving cross-language declaration merging. The C# `sample-parallel-order-checks` path deterministically publishes payment and inventory checks from one consumed message so the retained run and D3 view always exercise an exact three-step fork. The `sample-fulfillment-handoff` path demonstrates a linear C# → Java → C# chain. Later diagnostic slices should extend these with deliberately unhealthy paths for missing routing, a bounded timeout expectation, and unexpected amplification. A dedicated Aspire workflow application becomes more valuable with the orchestration runtime, when it can demonstrate persisted saga state, timeouts, recovery, and Dashboard state-machine views. That later environment should also include an event leaving the orchestrator boundary for a choreographed reaction, proving that both coordination relationships can coexist.

## Recommended Delivery Sequence

1. Specify configured, declared, observed, and causal relationships plus their confidence levels.
2. Add canonical declaration fixtures and matching C# and Java builders (**implemented first slice**); add diagnostic fixtures before choosing their final APIs.
3. Extend the monitoring observation contract with message, initiator, causation, operation, and parent-span identities; message and causal identity are implemented.
4. Extend exact producer-to-consumer matching into explicit consume-to-outbound causation (**implemented first slice**).
5. Attach the implemented declaration builders to registration and add normalized topology relationships (**implemented first slice**).
6. Assemble exact bounded causal chains into a monitoring-owned workflow-run projection, including step/consumer mapping, separate handler and handoff duration, outgoing operations, retries, and failures (**implemented first exact-only view**).
7. Add the live-updating workflow-run flow diagram and ordered step drill-down to the focused Dashboard view (**implemented first view**); more complex samples and richer branch layout remain.
8. Implement broader graph comparison and diagnostics in the monitoring service with completeness gates. The aggregate exact reaction overlay and first per-run comparison are implemented: exact send, publish, and terminal evidence is evaluated against declared count and timing intent, while expected absence is suppressed until inactivity and complete coverage make it supportable. Heuristic correlation, formal joins, cycle or amplification analysis, and cross-run baselines remain.
9. Explore bounded recurring-pattern discovery and breakout visualization only after exact causal chains and comparison evidence are trustworthy.
10. Evaluate demand for an explicit per-conversation lifecycle model only after the bounded diagnostic model has production evidence.

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
