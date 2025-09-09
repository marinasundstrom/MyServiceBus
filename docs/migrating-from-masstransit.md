# Migrating from MassTransit to MyServiceBus

MassTransit compatibility is a core design goal of MyServiceBus, but migration requires a few adjustments.
The following checklist highlights common areas to evaluate when switching:

- **Message contracts** – existing MassTransit contracts interoperate, yet confirm that custom serializers or
  headers map to MyServiceBus equivalents.
- **Bus registration** – replace `AddMassTransit` with `AddServiceBus` and move transport setup into the
  provided configurators.
- **Unified bus interface** – MyServiceBus uses `IMessageBus` for send, publish, and request/response
  instead of `IBus` and `IBusControl`.
- **Request clients** – create request clients through `IRequestClientFactory` or `RequestClientFactory` rather than
  MassTransit's extension methods.
- **Exception handling** – adopt `[Throws]` attributes and catch exceptions locally as required by the CheckedExceptions analyzer.
- **Java parity** – if services mix C# and Java clients, ensure consumers and contracts are exercised in both runtimes
  during migration.

## MassTransit features not yet available

MyServiceBus currently targets a minimal feature set. When migrating you will lose some advanced MassTransit capabilities:

- **Saga state machines** – long-running workflows and saga repositories are not included.
- **Message scheduling** – no built-in delayed delivery or scheduling API.
- **Outbox/inbox patterns** – transactional persistence helpers are omitted.
- **Alternative transports** – only RabbitMQ and the in-memory mediator are implemented.

Consult [docs/masstransit-differences.md](masstransit-differences.md) for a side-by-side comparison of behaviors.
