# Frequently Asked Questions (FAQ)

## What is MyServiceBus?

MyServiceBus is a lightweight service-bus runtime for Java and .NET that provides a consistent, opinionated broker-backed messaging model without requiring a particular application framework.

It defines a stable set of messaging concepts—such as publishing, sending, consumers, request/response, retries, middleware, scheduling, and testing—and keeps those semantics consistent across its C# and Java clients.

MyServiceBus is inspired by MassTransit and has verified compatibility for the documented RabbitMQ scenarios. It does not claim complete MassTransit feature or source compatibility.

Unlike most Java messaging solutions, MyServiceBus does **not require a framework-wide commitment** (such as Spring). It can be used as a self-contained runtime, integrated into existing applications, or composed using factories and decorators, depending on the needs of the project.

---

## What kind of projects is MyServiceBus for?

MyServiceBus is designed for projects that need a focused broker-backed messaging model, especially in mixed .NET and Java environments.

It is particularly well suited for:

### Cross-platform systems (.NET + Java)

MyServiceBus is compatible with **MassTransit’s messaging conventions**, making it easier for Java services to participate in systems originally built around .NET and C#.

This allows:

* Java services to consume messages published by MassTransit
* Java services to publish messages that .NET services understand
* Shared messaging semantics across platforms

### Teams working across .NET and Java

For teams that develop services in both ecosystems, MyServiceBus provides a **shared mental model**:

* The same concepts (`publish`, `send`, consumers, request/response)
* Similar runtime behavior
* Comparable testing and configuration approaches

This reduces cognitive overhead when moving between C# and Java codebases.

### Developers coming from .NET

MyServiceBus intentionally mirrors many .NET messaging patterns, making it approachable for developers with a C# background who are working in Java.

It offers:

* Familiar messaging semantics
* Explicit composition instead of annotation-driven magic
* A DI and logging model inspired by .NET infrastructure libraries

### Broker-backed messaging architectures

MyServiceBus is a good fit when:

* You want portable application concepts without hiding broker capabilities
* You need the supported RabbitMQ interoperability profile
* You want consistent behavior across C# and Java services

### Testable, infrastructure-aware services

Projects that value:

* In-memory testing without brokers
* Explicit lifecycles and scopes
* Predictable middleware and error handling

will benefit from MyServiceBus’s runtime-centric design.

---

### Short summary

> MyServiceBus is for teams building broker-backed systems across Java and .NET, especially where documented MassTransit interoperability and a shared messaging model matter.

---

## Why was MyServiceBus created?

MyServiceBus was created to provide an **opinionated but consistent, framework-independent alternative** to existing messaging solutions in Java.

Most asynchronous messaging offerings in the Java ecosystem are tightly coupled to application frameworks such as Spring. While these integrations are powerful, they often require an **all-or-nothing commitment** to a specific framework and its programming model.

MyServiceBus takes a different approach: it defines a standalone messaging runtime that can be used both with and without larger application frameworks.

### Inspired by MassTransit

The design of MyServiceBus is heavily influenced by **MassTransit**. It builds on the same core abstractions and follows similar messaging concepts and runtime behavior.

As a result, MyServiceBus is:

* Conceptually aligned with MassTransit
* Largely compatible in terms of behavior and semantics
* Familiar to developers coming from a .NET background

This makes it easier to build systems where Java and .NET services communicate using a shared messaging model.

### Motivation

The project aims to provide a smaller, community-driven option for teams that need basic MassTransit-style scenarios without adopting the breadth of an enterprise service-bus product.

This motivation also led to the creation of an accompanying **C# implementation**, ensuring that the same abstractions and concepts are available on both sides of the Java/.NET boundary.

---

### Short summary

> MyServiceBus offers a focused, framework-independent messaging runtime for Java and .NET with a documented MassTransit-compatible RabbitMQ profile.

---

## Why not use Spring for messaging?

Spring provides excellent integrations for specific messaging technologies (such as Spring Kafka, Spring AMQP, and Spring JMS), but it does **not provide a transport-agnostic messaging model**.

Spring’s messaging support is **adapter-based**, not model-based.

### Spring is transport-centric

In Spring:

* You choose the transport first (Kafka, RabbitMQ, JMS, etc.)
* The programming model, configuration, and semantics follow that choice
* Switching transports typically means rewriting configuration and often application code

Spring abstractions tend to mirror the underlying broker rather than define a stable, broker-independent messaging model.

### MyServiceBus is model-centric

MyServiceBus defines a consistent **application-level messaging model**:

* `publish` vs `send`
* Consumers
* Request/response
* Retries and error routing
* Middleware / filters
* Scheduling
* In-memory testing

The portable application concepts stay recognizable across supported transport profiles. Broker-specific topology, capabilities, and delivery guarantees remain explicit rather than being reduced to a lowest-common-denominator abstraction.

### Messaging semantics are part of the framework

Spring leaves many messaging semantics to:

* Broker configuration
* Annotations
* Framework defaults
* Application-specific conventions

MyServiceBus makes these semantics explicit and part of the framework itself, ensuring predictable behavior across its supported clients and declared transport profiles.

### Testing and tooling are first-class concerns

Spring messaging tests often depend on:

* Embedded brokers
* Testcontainers
* Extensive framework setup

MyServiceBus includes an in-memory test harness that exercises the same messaging pipeline as production, without requiring a broker or framework integration.

### Spring is a good integration layer, not a messaging runtime

Spring excels at:

* Application wiring
* Configuration
* Lifecycle management
* Integrating disparate libraries

MyServiceBus focuses on:

* Messaging runtime behavior
* Transport abstraction
* Consistent semantics
* Cross-cutting messaging concerns

The two are complementary, not mutually exclusive.

---

### Short answer

> Spring provides excellent technology-specific messaging integrations.
> MyServiceBus provides a focused service-bus model with explicit transport capabilities and matching C# and Java semantics.

---

## Why does MyServiceBus define its own `ServiceProvider`?

The Java implementation defines a small `ServiceProvider` abstraction to support the **internal composition needs of the service bus runtime**.

MyServiceBus relies on well-defined resolution semantics for:

* Consumer creation and lifetimes
* Scoped dependencies per message
* Middleware / filter pipelines
* Transport-agnostic runtime behavior
* In-memory testing and harnesses

In .NET, these semantics are naturally provided by the platform’s dependency injection model. In Java, dependency injection is **not a platform-level assumption**, and there is no single, standard IoC container.

To avoid coupling the core runtime to a specific framework, MyServiceBus defines a **minimal abstraction** that expresses only what the bus itself needs in order to function predictably.

**Google Guice is used as the default implementation** of `ServiceProvider`. It was chosen because it is lightweight, code-first, and well suited for programmatic composition. This is an implementation detail; applications are not required to adopt Guice as their primary DI framework, and application code should stick to MyServiceBus DI contracts plus standard `javax.inject` annotations.

MyServiceBus can be used in multiple ways:

* Via a factory-created, self-contained ServiceBus instance (with an internal `ServiceProvider`)
* Through decorator-based configuration
* By integrating with an external `ServiceCollection` or application composition root

All approaches result in the same runtime behavior.

---

## Why does MyServiceBus define its own `LoggerFactory`?

MyServiceBus defines a `LoggerFactory` abstraction to keep the core runtime independent of any specific Java logging framework.

The Java logging ecosystem is diverse (SLF4J, Log4j, Logback, JUL, etc.), and libraries typically either hard-depend on one API or assume that the surrounding framework provides logging.

MyServiceBus instead defines a small logging abstraction so that:

* Core components and transports can log consistently
* Logging behavior can be adapted without changing the runtime
* The framework remains usable outside of any particular application stack

**SLF4J is used as the default logging implementation**, allowing MyServiceBus to integrate naturally with most Java applications and logging backends.

This approach mirrors the .NET model (`ILogger` / `ILoggerFactory`), where logging is treated as an infrastructure concern rather than a concrete dependency.

---

### Short summary

> MyServiceBus defines minimal abstractions for dependency resolution and logging to support a predictable runtime across its C# and Java clients.
> Guice is the default DI implementation, and SLF4J is the default logging implementation, but neither is a hard requirement for application architecture.
