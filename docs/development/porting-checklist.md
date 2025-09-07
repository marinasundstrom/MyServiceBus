# Porting Checklist for MyServiceBus

- **Confirm prerequisites**: Ensure build tools, package managers, and a compatible runtime exist for the target platform.
- **Understand repository architecture**: Study the existing C# and Java clients to learn message flows and core abstractions.
- **Review design and architecture docs**: Consult the specifications, transport design, and design guidelines to understand the API surface and align with existing expectations.
- **Assess feature parity**: Catalog current features (serialization, routing, retries, telemetry) and determine how to provide them on the new platform.
- **Implement required transports**: Provide a RabbitMQ transport and an in-memory mediator equivalent to the C# and Java implementations.
- **Set up test harness**: Adapt the shared test harness so transport behavior can be verified consistently.
- **Provision infrastructure**: Configure a RabbitMQ broker and in-memory mediator for the platform and its tests.
- **Port core messaging features**: Implement publish/subscribe and request/response patterns, retries, and metrics in a way that fits platform conventions.
- **Handle errors**: Replicate or adapt error-handling semantics as needed; checked exception support can be deferred.
- **Integrate logging**: Use the platform's standard logging abstraction and ensure consumer failures are logged instead of crashing the process.
- **Establish CI**: Set up a continuous integration pipeline to build the new client, run its tests, and enforce formatting.
- **Document and validate**: Update the quick start guide and feature walkthrough and add tests mirroring existing ones to verify feature parity.

