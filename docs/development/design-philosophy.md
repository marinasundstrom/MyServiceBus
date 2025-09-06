# Design Philosophy

MyServiceBus aims for concept and behavior compatibility with MassTransit. Familiar primitives such as sending, publishing, consuming, and request/response behave the same.

APIs stay similar across platforms but are adapted to language idioms. Configuration details may differ—for example, Java lacks extension methods and instead relies on builders—while retaining the core messaging model.

See the following documents for detailed discussion of the design and platform differences:

- [Design Decisions](design-decisions.md)
- [Implementation Comparison](implementation-comparison.md)
- [C# and Java Client Feature Parity](csharp-java-parity.md)

