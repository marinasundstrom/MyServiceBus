# Design Guidelines

To keep the C# and Java clients aligned, follow these guidelines:

- **Preserve architectural parity**: Mirror pipeline stages, configuration patterns (the fluent configuration pattern and the factory pattern), and message handling semantics between the implementations whenever possible.
- **Maintain feature parity**: Introduce new capabilities to both clients in tandem. If a feature ships in one client first, document the gap in [csharp-java-parity.md](csharp-java-parity.md) and track it for the other language.
- **Align APIs**: Keep the public surface area similar across languages, adjusting only for idiomatic differences. See [API Design Guidelines](api-design-guidelines.md) for guidance on what to expose.
- **Document differences**: When divergence is unavoidable, clearly explain the rationale and differences in the documentation.

These guidelines help ensure a consistent developer experience regardless of language.
