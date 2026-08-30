# Union-Typed Consumers Proposal

## Status

Draft; investigation only. This proposal is not a supported API commitment.

Target: MyServiceBus for .NET 11 / C# 15, with an idiomatic Raven projection over the same .NET runtime contracts.

Feature area: consumer-method discovery, binding, dispatch, endpoint topology, and related request/response result handling.

### Executable prototype

An executable spike lives in [`test/Experiments/DotNet11Unions`](../../test/Experiments/DotNet11Unions/README.md). It currently proves the following against .NET 11 Preview 7:

- a C# named union emits `UnionAttribute`, implements `IUnion`, and exposes one public constructor per case;
- reflection discovery can normalize that carrier into one concrete registration per case while retaining one receive endpoint;
- mediator delivery of either case constructs the local union and invokes the same exhaustive handler;
- the union carrier is absent from message topology;
- Raven's `T1 | T2` source form lowers to Raven.Core's `System.Union<T1, T2>` and follows the same path without Raven-specific MyServiceBus runtime code;
- `Response<T1, T2>` can provide implicit case conversion, exhaustive C# matching, `TryGetValue`, and transparent STJ serialization on the `net11.0` build; and
- independently restored C# and Raven applications can consume locally packed MyServiceBus NuGet packages and execute these behaviors.

The prototype is intentionally narrower than this proposal. It supports reflection-discovered input unions with public case constructors, mediator dispatch, and two-case response results. It does not yet implement generated registration, NativeAOT, broker conformance, union-valued handler returns, typed `ConsumeContext<TUnion>`, provider-based ABI construction, general outbound union values, or a Java convenience API.

### What the experiment establishes

The result should be read by layer:

| Layer | Prototype conclusion |
| --- | --- |
| Portable messaging capability | One operation may declare several accepted contracts; every case keeps its ordinary identity, envelope, serialization contract, topology, failure behavior, and cross-language interoperability. |
| .NET runtime adaptation | A cached descriptor can recognize the standard union ABI and expand it before the ordinary strongly typed consume pipeline is closed. |
| C# projection | Named unions and union-semantic `Response<T...>` provide concise exhaustive APIs as an alternative to separate handlers or inheritance hierarchies. |
| Raven projection | Raven's standard `System.Union<T...>` works through the same ABI adapter and can expose Raven-native `match` syntax. |
| Java participation | Java requires no union feature to exchange any case. A future Java API may project the portable capability through sealed types or a registration builder, but that is a separate design choice. |

This separation is deliberate. A successful platform experiment may justify an idiomatic client feature without requiring every client language to copy its syntax or local carrier type.

### Compatibility with future runtime optimization

Union support should normalize into the same concrete registration model used by ordinary consumers. The transport, deserializer, filters, retry pipeline, and settlement path continue to operate on `TCase`; only the final method adapter constructs `TUnion`. This keeps the feature compatible with later runtime work because union awareness does not spread through the hot transport path.

The prototype reflection path performs metadata inspection once, caches the descriptor by carrier type, and compiles one constructor delegate per case. It does not rediscover constructors for each delivery. It still inherits the allocation and reflection costs of the existing reflection consumer path: the union struct is boxed for `MethodInfo.Invoke`, and invocation uses an argument array.

The optimized path should teach the C# registration generator to emit the normalized case registrations and direct construction:

```text
case registration: SubmitOrder
deserialize:        SubmitOrder
invoke:             Consume(new OrderCommand(context.Message), ...)
```

That generated path needs neither runtime ABI discovery, expression compilation, boxing, nor `MethodInfo.Invoke`. Raven source is not visible to the C# generator, so an optimized Raven/AOT path would need a Raven-generated registration manifest or equivalent compiler integration. Both optimized paths must produce the same concrete descriptors and topology as reflection discovery.

## Summary

MyServiceBus should allow one consumer method to accept a closed union of message types. Every union case remains an independent wire-level message contract and contributes its ordinary transport binding, while all cases are delivered through one receive endpoint and dispatched to one method.

The defining rule is:

> A union-typed consumer parameter registers each union case as an independent message contract on the method's endpoint. MyServiceBus preserves the concrete message identity through transport and middleware, constructs the union only for method invocation, and invokes the method once for the selected case.

The same union-aware binding infrastructure should support a request consumer returning one of several response contracts. MyServiceBus awaits the method, unwraps the active union case, and sends that case through the existing response pipeline.

This proposal also records a companion .NET 11 change: the existing C# `Response<T...>` classes should implement the standard custom-union ABI so C# and Raven callers can match response alternatives directly. MyServiceBus continues to own and implement those classes in C#; no Raven-specific response wrapper is introduced.

The broader runtime and language ideas remain summarized in [Future Runtime and Language Modernization](../development/future-runtime-modernization.md). This proposal owns the detailed union-consumer semantics.

Union consumers are also an example of the project's longer-term evolution. MyServiceBus begins from MassTransit-compatible fundamentals, but its public APIs should not remain limited to the expression forms available in MassTransit. Once the shared messaging semantics are stable, each client should become increasingly idiomatic for its platform while preserving wire compatibility and equivalent behavior. C# named unions, Raven ad-hoc unions, and a future Java sealed or builder-based projection can therefore represent the same portable capability without pretending to be the same API.

## Motivation

Existing consumer methods have one message parameter and therefore one message entry point. That is appropriate when messages have unrelated behavior or configuration. It is repetitive when several commands or events:

- form one closed application protocol;
- use the same receive endpoint and processing lane;
- share authorization, tracing, state loading, validation, or locking;
- need one local exhaustive decision; and
- differ only in the final domain operation.

A union-typed consumer makes the grouping explicit without turning the union carrier into a distributed contract.

```csharp
public union OrderCommand(
    SubmitOrder,
    AmendOrder,
    CancelOrder);

public static class OrderConsumers
{
    [Consumer("orders")]
    public static Task Consume(
        OrderCommand command,
        OrderService orders,
        ConsumeContext context) => command switch
        {
            SubmitOrder value => orders.Submit(value, context.CancellationToken),
            AmendOrder value => orders.Amend(value, context.CancellationToken),
            CancelOrder value => orders.Cancel(value, context.CancellationToken),
        };
}
```

The union supplies a closed accepted-input set and exhaustive local matching. MyServiceBus supplies transport routing, deserialization, scoped activation, retry, settlement, faulting, and topology.

### Unions and closed hierarchies

Unions provide a second way to express a closed set of variants; they are not merely shorter syntax for an inheritance hierarchy.

| Model | How the set is formed | Best fit |
| --- | --- | --- |
| Union | A declaration or use site composes otherwise independent types into a closed set. The variants do not need a common application base type. | Unrelated message contracts that one operation wants to accept or return together. |
| Closed hierarchy | A base interface or class owns a closed family of permitted derived types. Variants participate through inheritance and the language's hierarchy rules. | Variants that share a genuine domain abstraction, behavior, or polymorphic contract. |

This distinction is especially useful at messaging boundaries. Message types may be owned by different packages, generated from schemas, shared with older applications, or already committed to another inheritance model. A consumer can group those contracts with an ad-hoc union without editing them, introducing a marker interface, or claiming that they are subtypes of a new wire contract. The same contract can participate in different unions at different API boundaries.

Closed hierarchies remain valuable when inheritance is part of the model. Upcoming language support can make such hierarchies closed and exhaustively matchable, but their expression still depends on a common base relationship and the relevant language and compiler rules. They should not be required merely to gain exhaustive handling of several independent messages.

MyServiceBus should therefore support both projections over the same portable idea of “one of these declared alternatives.” Use a hierarchy when the variants are related by domain substitution; use a union when the relationship belongs to the consuming or responding operation. In either case, MyServiceBus must continue to route and serialize the concrete message contract rather than inventing a carrier contract.

## Goals

- Allow one consumer method to handle a closed set of message contracts.
- Support well-formed .NET 11 union types, including C# named unions.
- Support Raven's standard `System.Union<T1, ...>` carriers for ad-hoc union parameters.
- Keep every union case authoritative for wire identity, serialization, topology, retry, faults, and observability.
- Attach every case to one endpoint using the method's normal endpoint-selection rules.
- Invoke the method exactly once for one selected delivery.
- Reuse the existing consumer-method descriptor and receive pipeline.
- Support union-valued asynchronous consumer responses without serializing the carrier.
- Preserve interoperability with Java, older .NET applications, MassTransit peers, and other producers that know only the individual contracts.
- Keep reflection and generated registration behavior equivalent where the source language and toolchain permit both.

## Non-goals

- Defining a union envelope or union message URN.
- Requiring producers to publish the union carrier.
- Treating union declaration order as a broker ordering guarantee.
- Delivering partition-key configuration as part of the initial feature.
- Adding header-parameter or payload-parameter binding that consumer methods do not otherwise support.
- Replacing polymorphic message interfaces, closed hierarchies, or inheritance-based routing where those relationships are intentional.
- Treating every type with a `Value` property as a union.
- Supporting open generic unions, runtime-generated case sets, or value-type message contracts in the first implementation.
- Defining Raven syntax or changing the Raven compiler.
- Adding synchronous response-returning consumer methods solely for unions.
- Making ordinary `Publish(union)` or `Send(union)` unwrap implicitly in the first implementation.

## Relationship to existing consumer methods

The existing [Consumer Method Dispatch](../development/consumer-method-dispatch.md) model remains authoritative:

- one user-authored consumer method;
- a startup-time consumer descriptor;
- one message parameter;
- optional framework context and cancellation parameters;
- remaining ordinary parameters resolved from the delivery dependency-injection scope;
- `void`, `Task`, and `ValueTask` for one-way methods; and
- `Task<TResponse>` or `ValueTask<TResponse>` for response-bearing methods.

Union support changes what the message parameter or asynchronous response result may represent. It does not define a separate consumer pipeline.

## Core semantics

Given:

```csharp
public union OrderCommand(SubmitOrder, AmendOrder, CancelOrder);

[Consumer("orders")]
public static Task Consume(OrderCommand command, OrderService orders);
```

discovery expands one source method into three case bindings:

| Wire message contract | Endpoint | Invocation target | Method argument |
| --- | --- | --- | --- |
| `SubmitOrder` | `orders` | `Consume` | `OrderCommand(SubmitOrder)` |
| `AmendOrder` | `orders` | `Consume` | `OrderCommand(AmendOrder)` |
| `CancelOrder` | `orders` | `Consume` | `OrderCommand(CancelOrder)` |

The union is a local dispatch carrier. It does not replace the case type in:

- envelope `messageType` URNs;
- broker entity names or bindings;
- serialization metadata;
- consume context metadata;
- retry and settlement state;
- logs, traces, metrics, or monitoring records;
- skipped or error messages; or
- `Fault<TMessage>` production.

The carrier is constructed only after the envelope has been selected and deserialized as the concrete case type.

## Language projections

### C# named union

The first C# projection uses a named .NET union:

```csharp
public union AccountCommand(EnableAccount, DisableAccount);

[Consumer("accounts")]
public static Task Handle(
    AccountCommand command,
    AccountService accounts,
    CancellationToken cancellationToken) => command switch
    {
        EnableAccount value => accounts.Enable(value, cancellationToken),
        DisableAccount value => accounts.Disable(value, cancellationToken),
    };
```

MyServiceBus consumes the public .NET union metadata and ABI. It must not inspect compiler-generated private fields or depend on one storage layout.

### Raven ad-hoc union

Raven lowers an ad-hoc union to a standard `System.Union<T1, ...>` carrier supplied by Raven.Core:

```raven
[Consumer("customer-cache")]
async func consume(
    message: CustomerCreated | CustomerEmailChanged,
    cache: CustomerCache
) -> Task {
    match message {
        CustomerCreated(let customer) => await cache.Add(customer)
        CustomerEmailChanged(let change) => await cache.UpdateEmail(change)
    }
}
```

In metadata, the message parameter is `System.Union<CustomerCreated, CustomerEmailChanged>`. MyServiceBus recognizes and expands that ordinary .NET type. The transport pipeline does not contain Raven-specific behavior.

Raven namespace-level functions may lower to static methods on compiler-generated CLR types. Reflection discovery should treat the declaring type as an implementation detail and use the attributed method, signature, and endpoint metadata normally.

### Java

Java producers and consumers continue to use the individual contracts. Java does not need to understand the .NET union carrier because it never crosses the wire.

A future Java grouping API could use sealed interfaces or an explicit registration builder, but it is not required for this proposal or wire compatibility.

## Union recognition

Union recognition should live in one internal, cached component rather than in each transport or serializer.

```text
UnionDescriptor
  carrier type
  ordered case types
  create(case type, value) -> carrier
  unwrap(carrier) -> active case
```

On .NET 11, a supported carrier must have `UnionAttribute` and satisfy the standard union-member pattern. Cases come from:

- public one-argument constructors on the carrier; or
- static `Create(TCase)` members on a directly nested public `IUnionMembers` provider.

`Value`, `HasValue`, and `TryGetValue(out TCase)` participate according to the standardized ABI. `TryGetValue` is an optimized access pattern, not the sole source of case identity.

For a Raven compatibility prototype targeting an earlier runtime, reflection may locate the compatibility `UnionAttribute` and `IUnion` definitions supplied by Raven.Core by full metadata name. It must then validate the complete expected shape before trusting the carrier. The shipping .NET 11 path should use the runtime contracts directly.

Recognition must reject:

- a missing marker or malformed ABI;
- no cases;
- an open generic carrier or open case;
- duplicate case types;
- nullable wrapper cases not supported by the message model;
- value-type cases while MyServiceBus messages remain reference-type constrained; and
- a carrier that cannot be constructed from every declared case.

Nested unions are an open design decision. The first implementation should reject them rather than flatten them implicitly.

## Discovery and descriptor expansion

At startup or generation time, MyServiceBus should:

1. Discover the method through the existing attribute, container, or fluent-registration rules.
2. Identify the first non-framework parameter as the message parameter using the existing binder.
3. Recognize whether the parameter is a supported union carrier.
4. Read and validate its closed case set.
5. Create one case binding for every distinct message contract.
6. Attach all case bindings to the method's one resolved endpoint.
7. Build one shared invocation plan that constructs the carrier and binds the remaining parameters.
8. Preserve the union carrier and method as inspection provenance without registering the carrier as a message.

The current reflection and generated paths reject the union carrier because generated union declarations are structs while consumer messages require reference types. Expansion must occur before that reference-type validation. Each case is then validated and registered through the ordinary `TCase : class` pipeline.

Conceptually:

```text
Consume(OrderCommand)
  -> binding<SubmitOrder>(value => Consume(new OrderCommand(value)))
  -> binding<AmendOrder>(value => Consume(new OrderCommand(value)))
  -> binding<CancelOrder>(value => Consume(new OrderCommand(value)))
```

The reflection path can cache constructor/factory delegates and closed generic case descriptors. C# generated registration should emit direct construction and direct calls.

The C# source generator cannot analyze Raven source. Raven consumers initially use reflection registration. A later trimmed or NativeAOT Raven path needs Raven compiler/macro integration or a language-neutral generated descriptor manifest.

## Parameter binding and context

The initial supported shape is:

```csharp
public static Task Consume(
    OrderCommand message,
    ConsumeContext context,
    OrderService service,
    CancellationToken cancellationToken);
```

| Parameter | Existing binding source |
| --- | --- |
| Union message | Concrete deserialized case wrapped in the union |
| `ConsumeContext` | Current non-generic delivery context |
| `CancellationToken` | Delivery cancellation |
| Remaining ordinary parameter | Delivery-scoped dependency injection |

`ConsumeContext<TUnion>` should not be supported initially. The actual wire message is a case type, and the current generic context resolves its `Message` through that wire type. A correct typed-union context would need an adapter whose `Message` property returns the constructed carrier while all delivery metadata remains concrete-case metadata.

The non-generic `ConsumeContext` already exposes send, publish, response, headers, cancellation, and delivery metadata. Message-specific data should normally be obtained by matching the union parameter rather than asking the context to rematerialize a case.

## Endpoint and topology expansion

All cases use one resolved endpoint name. Expansion happens before transport topology is materialized:

```text
Consume(SubmitOrder | AmendOrder | CancelOrder)
  endpoint: orders
  consumes: SubmitOrder, AmendOrder, CancelOrder
  binds:    entity(SubmitOrder), entity(AmendOrder), entity(CancelOrder)
  excludes: entity(union carrier)
```

| Runtime or transport | Expanded behavior |
| --- | --- |
| Mediator/in-memory | Add the same invocation adapter under every case in the handler map. |
| RabbitMQ | Bind the endpoint queue through each case's ordinary exchange topology. |
| Azure Service Bus | Apply each case's ordinary entity/subscription mapping for the profile. |
| Amazon SQS/SNS | Apply each case's ordinary queue/topic subscription mapping for the profile. |
| Other profiles | Produce exactly the topology that separate consumer methods for the same cases and endpoint would produce. |

Topology invariants are:

- The carrier never becomes a message registration, entity, subscription, or message URN.
- Preserve case declaration order for deterministic descriptors, diagnostics, and inspection; broker correctness cannot depend on that order.
- Deduplicate identical case bindings within one method and endpoint.
- Do not coalesce separate application consumer methods that happen to consume the same case on the same endpoint; preserve existing multiple-consumer dispatch semantics.
- Apply entity-name overrides, implemented-contract bindings, exclude-from-topology rules, and transport capabilities per concrete case.
- Inspection shows the endpoint consuming the concrete cases and may show the union as grouping provenance.
- Failure topology remains associated with the endpoint and original delivery.

This is subscription expansion, not message fan-out. One delivery invokes the union method once. A message advertising multiple implemented contract URNs must not cause repeated invocation of the same union method.

Sharing one endpoint gives all cases one arrival queue and common endpoint concurrency. It does not guarantee sequential completion. Ordering, concurrency limits, and future partition-key configuration remain separate endpoint concerns.

## Union-valued consumer responses

A request consumer may return one of several valid response contracts:

```raven
[Consumer("check-order")]
async func checkOrder(
    request: CheckOrderStatus,
    orders: OrderRepository
) -> Task<OrderStatus | OrderNotFound> {
    let order = await orders.Find(request.OrderId)

    return order match {
        Some(let value) => OrderStatus(value.State)
        None => OrderNotFound(request.OrderId)
    }
}
```

The emitted result is `Task<System.Union<OrderStatus, OrderNotFound>>`. Discovery should inspect the asynchronous result before rejecting it as a value-type response carrier. The response handler:

1. awaits the existing `Task<T>` or `ValueTask<T>` shape;
2. rejects an inactive/default carrier;
3. unwraps the active case;
4. validates that it is a declared response case; and
5. sends it through the existing `ConsumeContext.RespondAsync` pipeline using the concrete response contract.

Request, correlation, conversation, response-address, tracing, and fault semantics remain unchanged. Throwing still produces the ordinary `Fault<TRequest>`. The union carrier is not serialized.

Synchronous union-valued returns are out of scope until synchronous response-returning methods are a general consumer-method capability.

## Companion: request-client response results

The request client already accepts multiple expected response types:

```csharp
Response<OrderStatus, OrderNotFound> response =
    await client.GetResponseAsync<OrderStatus, OrderNotFound>(request);
```

The `net11.0` abstractions asset should make the existing C# `Response<T>` and `Response<T1, T2>` classes well-formed custom unions:

- apply `System.Runtime.CompilerServices.UnionAttribute`;
- implement `IUnion`;
- provide one public construction member per variant;
- retain a discriminator assigned when the request client selects the response;
- expose `Value`;
- expose `HasValue` and `TryGetValue(out TCase)`; and
- retain `Message`, `FromT1`, `FromT2`, `Is`, and baseline-neutral `Match` members for compatibility.

This lets C# use transparent exhaustive switches and Raven use ordinary `match` over the C#-implemented response class. It does not add response topology: the request client already declares every expected response contract and its fault handling.

Generic alternatives that are identical or assignable require an explicit rule. The recommended portable behavior is to reject overlapping alternatives when the request is created and from every public union construction path. A discriminator cannot make transparent type matching unambiguous when the case types overlap.

## Serialization and outbound union values

Individual contracts remain authoritative. When `SubmitOrder` is received or returned:

- serialize and deserialize `SubmitOrder`;
- use its message URN and entity name;
- retain it in headers, traces, metrics, faults, error transport, and inspection; and
- construct or unwrap the union only inside the local API boundary.

Ordinary `Publish(unionValue)` and `Send(unionValue)` should not gain implicit unwrapping in the first implementation. A later explicit operation could unwrap and dispatch using the active case. If implicit behavior is considered, it needs separate API review because generic overload resolution might otherwise publish the carrier type.

Any outbound union operation must reject an inactive/default carrier before acquiring the transport and must never write the carrier into `messageType`.

### .NET 11 `System.Text.Json` behavior

Union serialization is not an ASP.NET Core-only feature. In .NET 11, `System.Text.Json` recognizes union contracts in both its reflection and source-generated modes. ASP.NET Core inherits that behavior on JSON request and response bodies because those paths use STJ. The same does not automatically apply to Newtonsoft.Json, MessagePack, or a transport serializer with its own contract model.

STJ's default union representation is transparent:

- writing a union unwraps it and writes only the active case using that case's JSON contract;
- it writes no union envelope, case tag, or `$type` discriminator;
- reading initially classifies the JSON by token shape; and
- cases with the same compatible shape, such as two object cases, require a `JsonTypeClassifier` configured through `[JsonUnion]` or serializer options.

That behavior is useful for HTTP APIs that want `anyOf`-like payloads without a synthetic wrapper, but it must not become MyServiceBus's case-selection mechanism. Most message DTOs begin with a JSON object token, so structural classification alone is commonly ambiguous. MyServiceBus already has stronger information in the envelope's concrete message identity.

The receive path should therefore:

1. select the concrete case from the message URN and registered contract metadata;
2. deserialize directly as that case type using the endpoint's configured serializer; and
3. construct the local union carrier after deserialization.

The send and response paths should perform the reverse: unwrap first, select the concrete message contract, and ask the configured serializer to write that case. This makes JSON emitted by STJ compatible with the transparent union payload shape without depending on STJ, and it gives non-STJ serializers the same MyServiceBus semantics.

Serializer-specific union classifiers remain application concerns for boundaries that genuinely deserialize a union value, such as an ASP.NET Core JSON body. They are not broker-routing metadata and must not be inferred from transport topology or copied into the portable envelope.

### `Response<T...>` serialization compatibility

Marking the existing `Response<T...>` classes as .NET unions may change how STJ serializes those wrappers outside MyServiceBus. Code that previously serialized the wrapper's public object shape may instead emit only the selected response message once STJ recognizes the union contract. MyServiceBus does not put the response wrapper on the broker wire, but applications may serialize it for HTTP responses, caches, logs, snapshots, or persistence.

Before adapting the existing classes, the implementation proof must compare their .NET 10 and .NET 11 STJ contracts in reflection and source-generated modes. If the change cannot preserve a documented compatibility promise, choose explicitly among:

- accepting and documenting the new transparent JSON representation for the `net11.0` asset;
- providing an opt-in converter or compatibility DTO for callers that need the old wrapper shape; or
- introducing a new union-semantic result type instead of changing the serialization contract of the existing class.

The decision must not add a response-wrapper representation to the MyServiceBus wire protocol. It is about application-facing serialization of a local result object.

## Filters, retry, faults, and observability

Method-level configuration applies to every case expanded from the method. Existing endpoint-level configuration also applies uniformly because all cases share the endpoint.

Per-case retry or filter configuration is not part of the current consumer-method API and is therefore deferred. If added later, it should attach to expanded case descriptors rather than branch inside transports.

A failed case retains its concrete identity:

- a failed `CancelOrder` produces `Fault<CancelOrder>` through ordinary behavior;
- retry and redelivery headers describe the original delivery;
- error and skipped messages preserve the original envelope; and
- logs and traces name `CancelOrder` as the message and may additionally name `OrderCommand` as the consumer grouping.

## Lifetimes, concurrency, and ordering

A static or Raven namespace-level method does not imply singleton services. Every service parameter is resolved from the existing delivery scope according to its registered lifetime.

All union cases share endpoint concurrency because they share one endpoint. They can execute concurrently unless normal endpoint configuration limits them.

Partitioning by a common aggregate key could complement union consumers, but no `[PartitionBy]` API currently exists. A future design would need a selector valid for every case and a startup or generated diagnostic when a case cannot supply the key. It is not part of this proposal's first delivery phase.

## Polymorphism and ambiguous cases

Unions and polymorphic message contracts solve different problems:

- a union is closed and supports exhaustive local handling;
- an interface or base contract is open and participates through existing polymorphic routing.

The first implementation should not flatten inheritance hierarchies within union cases. A delivery must select exactly one union case after applying the existing contract-identity rules. Reject union definitions whose cases are identical or assignable when they would make transparent matching or dispatch ambiguous.

If interface cases are permitted later, the proposal must define precedence between an exact concrete case and an implemented interface case and prove that one envelope invokes the method once.

## Validation and diagnostics

Reflection startup errors and generated diagnostics should identify the method, carrier, case, and endpoint where applicable. Cover at least:

- malformed or unrecognized union ABI;
- no cases, open cases, duplicate cases, or nested unions;
- unsupported value-type message or response cases;
- identical or assignable cases;
- more than one possible message parameter;
- `ConsumeContext<T>` whose type does not match the supported binding model;
- a case that is not a legal message contract;
- failure to construct the carrier from a case;
- an inactive/default carrier returned by a consumer;
- a runtime/target asset without the required union contracts; and
- duplicate expansion of one method/case/endpoint binding.

Unsupported behavior must fail during generation or startup when possible, not after broker delivery.

## Trimming, NativeAOT, and generation

Reflection discovery closes generic descriptors and invokes union construction dynamically. It retains the same trimming and NativeAOT limitations already documented for reflection consumer methods.

The C# source generator should:

- recognize the standard union symbol and enumerate cases;
- emit one typed registration per case;
- call the case constructor or provider directly;
- call the consumer method directly; and
- unwrap union-valued responses without `MethodInfo.Invoke`.

Raven source is not part of the Roslyn C# compilation, so the C# generator cannot discover Raven functions. A NativeAOT Raven solution needs Raven-owned generation or a shared descriptor manifest. That is a later integration, not a reason to put Raven-specific reflection in transports.

## Compatibility

### Existing applications

`IConsumer<T>`, single-message consumer methods, class-level consumer discovery, endpoint naming, and ordinary request handlers remain unchanged. Union consumers are additive.

### Wire compatibility

Java, older .NET clients, and MassTransit peers exchange the concrete cases. They do not need the union type or a translation layer. The compatibility claim remains bounded by each transport profile.

### Package and target strategy

Prefer multi-targeting the existing MyServiceBus packages rather than creating a new union package unless build evidence requires a separate asset. A possible transition is:

- `net10.0`: existing APIs plus baseline-neutral `Match`; no direct runtime union ABI dependency;
- `net11.0`: union-aware discovery and C# response classes implementing the standard ABI.

Raven.Core compatibility-carrier recognition on .NET 10 can be an experiment. It should not distort the stable .NET 11 design or introduce duplicate `System.Runtime.CompilerServices` contract definitions into MyServiceBus.

## Proposed implementation phases

### Phase 0: ABI and descriptor proof

- Compile representative C# named and Raven ad-hoc unions.
- Verify public metadata, constructor/provider discovery, `Value`, `HasValue`, and `TryGetValue` behavior.
- Verify STJ reflection and source-generated serialization for named unions, Raven carriers, and `Response<T1, T2>`, including ambiguous object-shaped cases and the previous response-wrapper JSON shape.
- Define `UnionDescriptor` and malformed-carrier diagnostics.
- Prove direct `Response<T1, T2>` construction and matching with C# and Raven package consumers.

### Phase 1: reflection consumer input

- Expand union message cases before reference-type validation.
- Register every case on one mediator endpoint.
- Construct the carrier and invoke once.
- Add RabbitMQ topology and delivery proof after mediator behavior is stable.

### Phase 2: generated C# input

- Teach the C# generator to emit the same expanded descriptors and direct invokers.
- Add diagnostics equivalent to reflection startup validation.
- Add trimming and NativeAOT package-smoke coverage.

### Phase 3: union-valued responses

- Recognize `Task<TUnion>` and `ValueTask<TUnion>` response shapes.
- Await, validate, unwrap, and respond with the selected case.
- Add request, fault, timeout, and cross-language tests.

### Phase 4: operational refinements

- Union-aware inspection provenance.
- Per-case configuration only if supported by the general descriptor model.
- Partition-key design only as a general endpoint feature.
- Explicit outbound union convenience after separate API review.

## Test matrix

### Discovery and invocation

1. Every case creates one binding on one endpoint.
2. Each concrete case invokes the same method with the correct active carrier value.
3. One envelope advertising several implemented case URNs invokes the method once.
4. Scoped services resolve through the ordinary delivery scope.
5. Cancellation and method exceptions follow the existing pipeline.
6. Existing single-message consumers are unaffected.

### Topology and transport

1. Inspection lists concrete case bindings and excludes the carrier as a message.
2. RabbitMQ, Azure Service Bus, and Amazon SQS/SNS materialize the same topology as separate case methods where those profiles support the contracts.
3. Duplicate cases do not create duplicate bindings or delivery.
4. Entity-name overrides and implemented-contract topology apply per case.
5. Java-published cases invoke the .NET union consumer without a union envelope.

### Failure behavior

1. Retry and terminal error handling retain the concrete case identity.
2. A failed case produces the ordinary concrete `Fault<TCase>`.
3. Malformed, nested, overlapping, open, or value-type cases fail before endpoint startup.
4. An inactive/default response carrier fails consumption and does not send an empty response.

### Request and response

1. A Raven union-valued handler returns each declared response to C# and Java requesters.
2. A C# union-valued handler returns each declared response to Raven.
3. Raven exhaustively matches the C#-implemented `Response<T1, T2>` from the staged `net11.0` package.
4. Fault, timeout, late response, and cleanup behavior remain unchanged.
5. STJ reflection and source-generated modes serialize the selected response case as deliberately documented.
6. Newtonsoft.Json, MessagePack, and transport serializers do not accidentally receive the union carrier when the selected case should be serialized.

## Alternatives considered

### One method per message

Already supported and preferable for unrelated behavior or distinct configuration. It does not provide one exhaustive dispatch point.

### Serialized union carrier

Rejected as the default. It couples producers to a closed case set, weakens message-specific broker routing, and complicates non-.NET interoperability.

### Marker interface or base type

Useful for an open polymorphic family, but it does not express a closed exhaustive input set and changes topology semantics.

### Middleware-only shared behavior

Useful for cross-cutting concerns, but it does not provide one local exhaustive domain decision or shared handler-local state.

### Raven-specific transport integration

Rejected. Raven emits ordinary .NET types and should use the same descriptor and transport pipeline.

## Open decisions

1. Should nested unions always be rejected or flattened by a documented normalization rule?
2. Should interface/base cases be prohibited initially or allowed with explicit exact-match precedence?
3. Should an explicit `PublishUnion`/`SendUnion` API be added after consumer support?
4. How should future per-case filters attach to one union method without inventing transport-specific configuration?
5. Should inspection expose the union name as consumer-protocol provenance?
6. Is a typed `ConsumeContext<TUnion>` adapter valuable enough to add?
7. What is the supported maximum arity, and should it follow Raven.Core's current `Union<T1, ...>` family or a general ABI limit?
8. Can the same descriptor manifest support Raven NativeAOT and other .NET languages?
9. Should named union carriers ever be accepted as explicitly serialized message contracts, or should that remain prohibited?
10. Is transparent STJ serialization of the existing `Response<T...>` classes an acceptable `net11.0` behavior change, or does it require a new result type or compatibility converter?

## References

- [.NET 11 union support in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/unions?view=aspnetcore-11.0)
- [.NET 11 library changes: C# union type serialization](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries#c-union-type-serialization)

- [Future Runtime and Language Modernization](../development/future-runtime-modernization.md)
- [Consumer Method Dispatch](../development/consumer-method-dispatch.md)
- [Transport Specification](../specs/transport-spec.md)
- [Compatibility Policy](../compatibility.md)
- [C# union types](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/union)
- [.NET 11 library changes](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries)
- [Raven programming language](https://github.com/marinasundstrom/raven)
- [Raven.Core standard `System.Union<T...>` declarations](https://github.com/marinasundstrom/raven/blob/main/src/Raven.Core/Union.rvn)
- [MassTransit consumers](https://masstransit.io/documentation/concepts/consumers)
- [MassTransit message contracts and polymorphism](https://masstransit.io/documentation/concepts/messages)
