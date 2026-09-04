# Consumer definitions

Consumer definitions are the policy layer between language-facing registration
and realized bus topology. They let an application associate stable endpoint
and execution choices with a consumer without embedding those choices in a
transport bootstrap block.

The intended registration pipeline is:

```text
language API → consumer registration → consumer definition → realized topology → runtime
```

The .NET and JVM implementations own separate code but preserve the same
definition semantics. Their architecture is deliberately asymmetric. On .NET,
the platform API and its C# projection are effectively one surface, so the
definition structure can remain familiar to MassTransit users. On the JVM,
the common definition and runtime layers sit below sibling Java and Kotlin
projections. Java exposes fluent objects, while Kotlin should expose receiver
DSLs, reified registration helpers, and suspending consumer functions without
inheriting Java's overload set or requiring Kotlin consumers to implement the
Java consumer interfaces.

The JVM definition model therefore records resolved consumer identity,
consumed message types, endpoint identity and naming metadata, and execution
policy independently from the invocation mechanism. A Java interface consumer,
a reflected Java method, and a Kotlin suspending function should lower into the
same definition shape. Runtime-only factories, reflected methods, continuations,
and callbacks belong in invocation adapters associated with that definition;
they are not definition data.

## Initial model

The first slice supports two transport-neutral policies:

- an explicit endpoint name;
- a concurrent message limit.

Each normalized definition also captures the consumer or handler identity,
every consumed message type, the resolved endpoint name, whether that name was
explicit, and the formatter type that supplied a conventional name. This
structural metadata is captured for interface consumers, typed registrations,
generated registrations, and consumer methods or functions. It does not depend
on the Java `Consumer<T>` interface being the invocation mechanism.

Definitions can be reusable classes or inline registration configuration. An
explicit receive-endpoint configuration takes precedence over the definition;
the definition takes precedence over transport defaults. The definition is
retained by the topology registry and attached to the consumer topology, while
its resolved values are copied into the mutable transport/runtime fields that
consume them today. This keeps inspection and future tooling on the stable
definition model while the runtime is migrated away from projection-specific
registration shapes.

The model will grow from this boundary. Consumer pipeline configuration,
endpoint definitions, retry and outbox policy, serializer selection,
transport-specific endpoint options, dependency-injected definition classes,
reflection discovery, and generated registration remain later slices. Those
features should extend the definition stage rather than add more state directly
to registration overloads or mutable topology objects.

Definitions describe policy; they do not define the language's execution
shape. A Java consumer may complete with `CompletionStage`, a Kotlin consumer
may suspend, and a C# consumer may return `Task` or `ValueTask`. Each language
projection supplies an invocation adapter, while definitions and the runtime
retain the same endpoint, topology, scope, and delivery behavior.
