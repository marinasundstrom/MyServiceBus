# Frequently Asked Questions (FAQ)

## What is MyServiceBus?

MyServiceBus is a **transport-agnostic messaging framework** for Java and .NET that provides a **consistent, opinionated application-level messaging model**, independent of any specific broker or application framework.

It defines a stable set of messaging concepts—such as publishing, sending, consumers, request/response, retries, middleware, scheduling, and testing—and keeps those semantics consistent across transports and platforms.

MyServiceBus is inspired by and largely compatible with **MassTransit**, making it well suited for systems where Java and .NET services need to communicate using shared messaging conventions.

Unlike most Java messaging solutions, MyServiceBus does **not require a framework-wide commitment** (such as Spring). It can be used as a self-contained runtime, integrated into existing applications, or composed using factories and decorators, depending on the needs of the project.

---

## What kind of projects is MyServiceBus for?

MyServiceBus is designed for projects that need a **consistent, transport-agnostic messaging model**, especially in mixed .NET and Java environments.

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

### Transport-agnostic messaging architectures

MyServiceBus is a good fit when:

* You want messaging logic decoupled from a specific broker
* You expect transports to change or evolve
* You want consistent behavior across environments

### Testable, infrastructure-aware services

Projects that value:

* In-memory testing without brokers
* Explicit lifecycles and scopes
* Predictable middleware and error handling

will benefit from MyServiceBus’s runtime-centric design.

---

### Short summary

> MyServiceBus is for teams building message-driven systems across Java and .NET, especially where compatibility with MassTransit, transport independence, and a shared messaging model matter.

---

## Why was MyServiceBus created?

MyServiceBus was created to provide an **opinionated but consistent, framework-independent alternative** to existing messaging solutions in Java.

Most asynchronous messaging offerings in the Java ecosystem are tightly coupled to application frameworks such as Spring. While these integrations are powerful, they often require an **all-or-nothing commitment** to a specific framework and its programming model.

MyServiceBus takes a different approach: it defines a **standalone messaging runtime** with a stable, transport-agnostic API that can be used both with and without larger application frameworks.

### Inspired by MassTransit

The design of MyServiceBus is heavily influenced by **MassTransit**. It builds on the same core abstractions and follows similar messaging concepts and runtime behavior.

As a result, MyServiceBus is:

* Conceptually aligned with MassTransit
* Largely compatible in terms of behavior and semantics
* Familiar to developers coming from a .NET background

This makes it easier to build systems where Java and .NET services communicate using a shared messaging model.

### Motivation

The project was motivated in part by the **recent commercialization of MassTransit** and the desire to preserve an open, framework-independent messaging model across platforms.

This motivation also led to the creation of an accompanying **C# implementation**, ensuring that the same abstractions and concepts are available on both sides of the Java/.NET boundary.

---

### Short summary

> MyServiceBus exists to offer a consistent, transport-agnostic messaging framework for Java, inspired by MassTransit, independent of application frameworks, and suitable for cross-platform systems.

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

This model stays the same regardless of the underlying transport.

The transport is an implementation detail.

### Messaging semantics are part of the framework

Spring leaves many messaging semantics to:

* Broker configuration
* Annotations
* Framework defaults
* Application-specific conventions

MyServiceBus makes these semantics explicit and part of the framework itself, ensuring predictable behavior across transports and environments.

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

> Spring provides excellent transport integrations, but not a transport-agnostic messaging model.
> MyServiceBus exists to define that model and keep it consistent across transports, environments, and tests.

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

**Google Guice is used as the default implementation** of `ServiceProvider`. It was chosen because it is lightweight, code-first, and well suited for programmatic composition. This is an implementation detail; applications are not required to adopt Guice as their primary DI framework.

MyServiceBus can be used in multiple ways:

* Via a factory-created, self-contained ServiceBus instance (with an internal `ServiceProvider`)
* Through decorator-based configuration
* By integrating with an external `ServiceCollection` or application composition root

All approaches result in the same runtime behavior.

---

## Why does MyServiceBus define its own `LoggingFactory`?

MyServiceBus defines a `LoggingFactory` abstraction to keep the core runtime **independent of any specific Java logging framework**.

The Java logging ecosystem is diverse (SLF4J, Log4j, Logback, JUL, etc.), and libraries typically either hard-depend on one API or assume that the surrounding framework provides logging.

MyServiceBus instead defines a small logging abstraction so that:

* Core components and transports can log consistently
* Logging behavior can be adapted without changing the runtime
* The framework remains usable outside of any particular application stack

**SLF4J is used as the default logging implementation**, allowing MyServiceBus to integrate naturally with most Java applications and logging backends.

This approach mirrors the .NET model (`ILogger` / `ILoggerFactory`), where logging is treated as an infrastructure concern rather than a concrete dependency.

---

### Short summary

> MyServiceBus defines minimal abstractions for dependency resolution and logging to support a predictable, transport-agnostic runtime.
> Guice is the default DI implementation, and SLF4J is the default logging implementation, but neither is a hard requirement for application architecture.