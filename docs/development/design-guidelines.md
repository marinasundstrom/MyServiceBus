# Design Guidelines

To keep the C# and Java clients aligned, follow these guidelines:

- **Preserve architectural parity**: Mirror pipeline stages, configuration patterns (the fluent configuration pattern and the factory pattern), and message handling semantics between the implementations whenever possible.
- **Maintain feature parity**: Introduce new capabilities to both clients in tandem. If a feature ships in one client first, document the gap in [csharp-java-parity.md](csharp-java-parity.md) and track it for the other language.
- **Align APIs**: Keep the public surface area similar across languages, adjusting only for idiomatic differences. See [API Design Guidelines](api-design-guidelines.md) for guidance on what to expose.
- **Measure behavioral parity, not syntactic parity**: C# may evolve a MassTransit-familiar API, while Java favors factories and Java-style fluent builders. Do not force matching overloads or construction patterns across languages.
- **Document differences**: When divergence is unavoidable, clearly explain the rationale and differences in the documentation.
- **Make divergence intentional**: Do not inherit legacy behavior solely for historical fidelity. Record the affected compatibility level, replacement behavior, and migration impact, and protect previously verified protocol behavior with conformance tests.
- **Correct before stabilizing**: During the current MassTransit-alignment phase, replace incompatible MyServiceBus wire behavior outright. Do not add aliases or fallback modes merely to preserve earlier MyServiceBus behavior; compatibility guarantees begin only when a stable protocol policy is explicitly declared.

These guidelines help ensure a consistent developer experience regardless of language.
