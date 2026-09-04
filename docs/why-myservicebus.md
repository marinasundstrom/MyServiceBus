# Why Choose MyServiceBus?

MyServiceBus is for teams that want a focused messaging runtime across C#, Java, and Kotlin. C# and Java are the stable reference projections for the current preview; Kotlin is an experimental sibling projection over the same JVM runtime. Its strongest fit is not “every feature for every system”; it is a documented common model for the messaging fundamentals that the reference clients and selected transport profile support.

## The Motivation

MyServiceBus starts from the parts of MassTransit that have proven useful—its messaging vocabulary, envelope conventions, consumer model, and operational semantics—and develops them in a direction suited to this project:

- first-class C# and Java implementations with matching behavior, plus an experimental idiomatic Kotlin projection
- generated consumer registration and direct invocation in both ecosystems
- a smaller portable core with explicit transport capabilities and delivery guarantees
- wire interoperability that supports coexistence rather than requiring an all-at-once migration
- a first-class mediator runtime that can replace MediatR for local commands, queries, and notifications
- continued permissive open-source licensing for the core runtime

“Improve upon MassTransit” does not mean claiming universal superiority or reproducing every feature. It means preserving the strong foundation, then improving the particular boundaries MyServiceBus owns while documenting where MassTransit remains the more capable or mature choice.

## Two Primary Adoption Paths

### Extend a MassTransit-Based .NET System with Java

An existing .NET estate may already use MassTransit successfully but need to introduce a Java service. MyServiceBus lets that Java service participate through the verified common protocol profile instead of requiring a custom bridge or a second set of messaging conventions.

This path is a good fit when the integration uses the documented fundamentals: send, publish, consume, request/response, compatible envelopes and headers, and the failure behavior verified for the selected transport. It is not a claim that the Java client implements every MassTransit feature. Check the [interoperability documentation](compatibility.md) and [differences from MassTransit](masstransit-differences.md) before committing to a design.

The currently verified MassTransit peer is pinned to version 8.5.1. That pin is an interoperability test boundary, not a licensing recommendation or a promise of compatibility with every MassTransit release.

### Start a New C#, Java, and/or Kotlin System

A greenfield system can choose MyServiceBus from the beginning and use the same concepts across .NET and the JVM. A team can select C# or Java per service without creating two unrelated messaging architectures. Kotlin services can select the experimental coroutine-native frontend while sharing Java's underlying JVM runtime and transports.

This path is strongest when the system needs a deliberately small service-bus model: typed contracts, consumers, send and publish intent, requests, retries, faults, testing, telemetry, and an explicit transport boundary. The application still owns service boundaries, idempotency, business recovery, security, and broker operations.

### Replace MediatR for In-Process Messaging

An application can use MyServiceBus solely as a mediator. Dedicated C# and Java handler interfaces support commands, queries, response-bearing handlers, and local notifications through the same scope, filter, retry, and telemetry model used elsewhere in MyServiceBus.

This is a primary adoption path rather than a testing convenience. The supplied C# source generator and Java annotation processor emit typed registrations and direct invokers, avoiding reflection-based handler discovery and method invocation on the generated path. The MIT-licensed core also avoids the commercial/reciprocal licensing introduced in MediatR 13 and later.

MassTransit supports in-process mediator functionality, but its product identity and broader design center are distributed message-based applications. MyServiceBus can use that distinction deliberately: be a focused MediatR replacement when all work is local, while offering a coherent route to broker-backed messaging when a boundary later crosses processes. See [Using MyServiceBus as a Mediator](mediator.md).

## Commercial Fit

MyServiceBus is MIT-licensed, and continued permissive open-source licensing of the core runtime is a project goal. As of MassTransit v9, MassTransit requires a commercial license; its commercial offering also provides maturity, support, and a much broader feature set. Those capabilities can be worth paying for when a system needs them.

For a project whose requirements are covered by the MyServiceBus common subset—or whose current stage does not yet justify that commercial commitment—MyServiceBus offers a smaller, permissively licensed option. The same rationale applies to teams evaluating current MediatR releases for in-process messaging. The trade-off is real: MyServiceBus is currently a preview project and does not provide the commercial support, product maturity, or full enterprise feature breadth of those established products.

See MassTransit's [official licensing documentation](https://masstransit.massient.com/configuration/license/) for the current MassTransit terms rather than relying on this comparison alone.

## When MyServiceBus Is Not the Better Choice

Choose MassTransit or another mature platform when commercial support, long-term product assurances, or an advanced feature outside the verified MyServiceBus boundary is required. Choose a native broker API when precise broker control is the primary concern and the application does not benefit from a shared service-bus model.

For production evaluation, review [Enterprise Production Readiness](enterprise-readiness.md) and the [Delivery Guarantees](specs/delivery-guarantees.md). Preview status should be treated as an engineering and support constraint, not hidden behind the licensing advantage.
