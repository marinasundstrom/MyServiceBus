# Pipeline and Filter Specification

## Purpose

MyServiceBus uses MassTransit-familiar pipes and filters as the portable middleware model for every client. C# and Java expose idiomatic APIs, but they must preserve the same observable execution semantics. A platform implementation must document an intentional difference rather than silently approximate another client's behavior.

This specification defines the stable fundamentals. It does not require clients to reproduce MassTransit's internal pipeline implementation or legacy extension surface.

## Concepts

- A **context** carries the operation state and cancellation signal through a pipeline.
- A **filter** receives a context and the next pipe segment.
- A **pipe** is an ordered composition of filters.
- A **configurator** records filters in registration order and builds an immutable pipe.

The corresponding public concepts are `PipeContext`, `IFilter<TContext>`/`Filter<TContext>`, `IPipe<TContext>`/`Pipe<TContext>`, and `PipeConfigurator<TContext>`.

## Execution contract

All clients must implement these rules:

1. The first registered filter is the outermost filter and is entered first.
2. Calling the next pipe segment transfers control to the next registered filter.
3. Code after the next pipe completes runs in reverse registration order.
4. A filter may deliberately short-circuit the pipeline by completing without calling the next pipe.
5. An unhandled downstream failure propagates through each outer filter and completes the pipeline with the same underlying failure.
6. The same context instance and cancellation signal flow through every filter invocation for one operation.
7. A filter may invoke the next pipe more than once. Retry behavior is implemented using this capability and must remain explicit.
8. Built pipes are immutable and safe to invoke concurrently. Whether a filter instance itself is safe for concurrent reuse depends on its documented lifetime.

`UseExecute`/`useExecute` is an ordered filter stage: it runs its callback and then invokes the next pipe. It is not a terminal handler.

## Operation pipelines

The portable model recognizes send, publish, and consume pipelines. Filters may be applicable to every operation of a context kind or to a specific message type where the client API supports that distinction. Equivalent client APIs must produce the same observable ordering and message outcome.

Publish executes its publish pipeline to completion before entering the send pipeline used by the resolved transport endpoint. The transport is invoked only after both application pipelines complete successfully. C# and Java use distinct `PublishContext` and `SendContext` filter types for these stages.

Transport-internal pipelines may add connection, topology, serialization, settlement, or acknowledgement stages. Those stages are transport capabilities and are not automatically part of the portable application-filter API.

## Failure and retry

A filter that catches a failure owns the decision to complete, transform, or rethrow it. Retry filters re-invoke only their downstream pipe segment. Terminal fault publication and error-transport behavior therefore run after configured retries are exhausted.

Application filters registered before a retry filter are entered once and observe only the terminal outcome. Filters registered after retry are entered again for every attempt and observe individual attempt failures. This ordering is identical in the C# and Java mediator runtimes.

When every attempt fails, the last underlying consumer failure remains the terminal failure. Upstream filters observe that failure once after retry is exhausted; downstream filters observe every failed attempt. Java exposes the terminal failure through its idiomatic `CompletionException` wrapper when a `CompletableFuture` is joined, while C# `await` surfaces the underlying exception directly.

Retry remains opt-in. Without a configured retry filter, a failing mediator consumer is invoked exactly once and its failure propagates immediately through the surrounding consume filters.

The initial portable retry profile supports immediate and fixed-delay attempts. Exception selection, attempt metadata, scope behavior, and redelivery are separate compatibility requirements and must not be implied until specified and tested.

Cancellation while waiting for a fixed-delay retry completes the operation promptly with the platform's cancellation exception and prevents another attempt. Java cancellation tokens support unregisterable callbacks so delayed pipeline work can provide the same observable behavior as cancellation-aware .NET tasks.

## Dependency injection and lifetime

Filter instances supplied directly by an application may be reused concurrently. Type-based filter registration may use dependency injection. Each client must expose and document whether such a filter is singleton, operation-scoped, or transient, and must dispose owned scopes predictably.

`UseScopedFilter<TFilter>` in C# and `useScopedFilter(FilterClass.class)` in Java resolve a registered filter from a new operation scope. The scope remains alive until the asynchronous downstream pipeline completes and is then disposed. Ordinary type-based `UseFilter<TFilter>`/`useFilter(FilterClass.class)` registration builds one filter instance with the pipe and must not be used when per-operation lifetime is required.

## Descriptor model

Every pipe configurator exposes an immutable, versioned `PipelineDescriptor`. Its ordered `FilterDescriptor` entries contain only stable configuration facts:

- zero-based registration order
- portable filter kind, initially `filter`, `execute`, or `retry`
- optional platform implementation type name for diagnostics
- lifetime: application-supplied instance, pipe lifetime, or operation scope
- immutable string configuration values such as retry count and delay

Descriptors never expose filter instances, dependency-injection providers, callbacks, delegates, or mutable configurator state. They are safe inputs for validation and future inspection projections. A later runtime integration layer will associate descriptors with send, publish, or consume operations and topology identities; the generic pipe configurator does not guess that operational context.

`PipelineDescriptor.CurrentVersion` in C# and `PipelineDescriptor.CURRENT_VERSION` in Java advance together when the serialized meaning or structure changes. Descriptor versioning is independent from topology snapshot and package versions.

## Conformance

C# and Java conformance tests must cover at least:

- registration and wrapping order
- short-circuit behavior
- exception propagation and downstream suppression
- cancellation-signal identity and visibility
- retry attempt count and downstream re-entry
- concurrent pipe invocation
- type-resolved filter lifetime and scope disposal
- integrated send, publish, and consume filter ordering

The isolated pipe tests establish the first five fundamentals. Runtime-level tests and scoped lifetime tests remain part of the mediator and in-memory stability gate.
