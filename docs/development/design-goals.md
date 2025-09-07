# MyServiceBus Design Goals

- **MassTransit Compatibility**: Ensure messages, protocol, and public APIs remain compatible with MassTransit so any client can communicate with MassTransit services.
- **MassTransit Familiarity in C#**: Developers coming from MassTransit should find the C# client familiar in its design and concepts.
- **Cross-Language Parity**: Moving between the C# and Java clients should require minimal adjustment; the API and behavior should remain consistent once language-specific design choices are accounted for.
- **Aligned Implementations**: The C# and Java codebases, including their test harnesses, should evolve together so that features and behavior stay in sync.
- **Owned Abstractions**: Present a MassTransit-like surface through a single `IMessageBus` and self-contained envelope, fault, and pipeline contracts so the bus can evolve independently of MassTransit.
- **Isolated Transports and Runtime Dependencies**: Keep brokers and runtime infrastructure behind pluggable adapters and rely on lightweight DI and logging abstractions so implementations can change without rippling through application code.
- **First-Class Extensibility**: Expose filter and pipeline registration, retry policies, and request client factories within MyServiceBus APIs rather than MassTransit extensions to allow future divergence in pipeline strategy.

See the [design philosophy](design-philosophy.md) for how these goals shape cross-platform APIs and configuration.

