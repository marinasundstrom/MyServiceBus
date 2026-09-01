# Porting Checklist for MyServiceBus

- **Confirm prerequisites**: Ensure build tools, package managers, and a compatible runtime exist for the target platform.
- **Start with concepts and behavior**: Derive message flow, identities, operations, and outcomes from the language-neutral specification and its profiles. Use the C# and Java clients as implementation evidence, not source code to translate.
- **Design an idiomatic API**: Choose native interfaces, types, asynchronous primitives, configuration, and lifecycle patterns for the target platform after the behavioral model is understood.
- **Assess feature parity**: Catalog current features (serialization, routing, retries, telemetry) and determine how to provide them on the new platform.
- **Implement required transports**: Provide a RabbitMQ transport and an in-memory mediator equivalent to the C# and Java implementations.
- **Set up test harness**: Adapt the shared test harness so transport behavior can be verified consistently.
- **Provision infrastructure**: Configure a RabbitMQ broker and in-memory mediator for the platform and its tests.
- **Implement core behavior**: Implement publish/subscribe and request/response behavior, retries, and telemetry in a way that fits platform conventions.
- **Handle errors**: Preserve specified failure categories and outcomes using idiomatic native exceptions; do not copy exception class hierarchies solely for API parity.
- **Integrate logging**: Use the platform's standard logging abstraction and ensure consumer failures are logged instead of crashing the process.
- **Establish CI**: Set up a continuous integration pipeline to build the new client, run its tests, and enforce formatting.
- **Document and validate**: Declare supported profiles, update the quick start guide, and pass shared fixtures and behavioral scenarios. Add API-specific tests for the implementation without making them portable requirements.
