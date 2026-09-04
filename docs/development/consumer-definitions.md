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
definition semantics. C# exposes properties and reusable definition classes;
Java exposes a fluent definition object. Kotlin should project a receiver DSL
onto the JVM model rather than inherit Java's registration overloads.

## Initial model

The first slice supports two transport-neutral policies:

- an explicit endpoint name;
- a concurrent message limit.

Definitions can be reusable classes or inline registration configuration. An
explicit receive-endpoint configuration takes precedence over the definition;
the definition takes precedence over transport defaults. The definition is
retained separately by the topology registry, while its resolved values are
copied into the consumer topology used by the runtime.

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
