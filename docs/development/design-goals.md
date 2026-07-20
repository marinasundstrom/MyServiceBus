# MyServiceBus Design Goals

- **Explicit MassTransit Compatibility**: Preserve wire compatibility and the portable messaging semantics needed for interoperability. Claim transport-profile compatibility only where addressing, topology, and settlement behavior are covered by conformance tests; source compatibility and complete MassTransit feature parity are not goals.
- **MassTransit Familiarity in C#**: Developers coming from MassTransit should find the C# client familiar in its design and concepts.
- **Cross-Language Parity**: Moving between the C# and Java clients should require minimal adjustment; the API and behavior should remain consistent once language-specific design choices are accounted for.
- **Aligned Implementations**: The C# and Java codebases, including their test harnesses, should evolve together so that features and behavior stay in sync.
- **Owned Abstractions**: Present a MassTransit-like surface through a single `IMessageBus` and self-contained envelope, fault, and pipeline contracts so the bus can evolve independently of MassTransit.
- **Isolated Transports and Runtime Dependencies**: Keep brokers and runtime infrastructure behind pluggable adapters and rely on lightweight DI and logging abstractions so implementations can change without rippling through application code.
- **First-Class Extensibility**: Expose filter and pipeline registration, retry policies, and request client factories within MyServiceBus APIs rather than MassTransit extensions to allow future divergence in pipeline strategy.
- **Capability-Aware Transports**: Model transport features as native, emulated, or unsupported so additional brokers and event streams retain their real delivery, ordering, settlement, and replay semantics.
- **Optional Operations Plane**: Keep inspection, monitoring, and dashboards outside the delivery-critical core and expose them through stable programmatic APIs.
- **Executable Specification**: Treat shared fixtures, cross-language tests, and transport-profile interoperability tests as the source of truth for every client and adapter.

See the [design philosophy](design-philosophy.md) for how these goals shape cross-platform APIs and configuration.
See the [architecture](../myservicebus-architecture.md) and [roadmap](../roadmap.md) for the intended system boundaries and delivery sequence.
