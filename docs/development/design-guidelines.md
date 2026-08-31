# Design Guidelines

To keep the C# and Java clients aligned, follow these guidelines:

- **Preserve architectural parity**: Mirror pipeline stages, configuration patterns (the fluent configuration pattern and the factory pattern), topology projection, message mapping, settlement decisions, and message handling semantics between implementations whenever possible. C# and Java should normally correspond closely; clients on less similar platforms should expose recognizable approximations of the same stages.
- **Maintain feature parity**: Introduce new capabilities to both clients in tandem. If a feature ships in one client first, document the gap in [csharp-java-parity.md](csharp-java-parity.md) and track it for the other language.
- **Align APIs**: Keep the public surface area similar across languages, adjusting only for idiomatic differences. See [API Design Guidelines](api-design-guidelines.md) for guidance on what to expose.
- **Measure behavioral parity, not syntactic parity**: C# may evolve a MassTransit-familiar API, while Java favors factories and Java-style fluent builders. Do not force matching overloads or construction patterns across languages.
- **Apply the compatibility hierarchy**: Decide in the order `concept compatibility → behavior compatibility → API compatibility (contracts + behavior) → idiomatic design`. A familiar interface is valuable only after its semantics fit MyServiceBus and its observable behavior is verified; platform idioms may change the surface but not the meaning or outcomes.
- **Use the modern idioms allowed by each supported baseline**: The current C# packages may use the .NET 10 BCL and current C# features. Java publishes Java 17-compatible bytecode and APIs while using Java 17 features and library types such as records, sealed types, `Instant`, `Duration`, and `CompletionStage` where they improve the contract. A newer JDK feature requires an explicit baseline decision rather than an accidental toolchain upgrade.
- **Document differences**: When divergence is unavoidable, clearly explain the rationale and differences in the documentation.
- **Make divergence intentional**: Do not inherit legacy behavior solely for historical fidelity. Record the affected compatibility level, replacement behavior, and migration impact, and protect previously verified protocol behavior with conformance tests.
- **Correct before stabilizing**: During the current MassTransit-alignment phase, replace incompatible MyServiceBus wire behavior outright. Do not add aliases or fallback modes merely to preserve earlier MyServiceBus behavior; compatibility guarantees begin only when a stable protocol policy is explicitly declared.

These guidelines help ensure a consistent developer experience regardless of language.
