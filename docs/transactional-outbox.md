# Transactional Outbox and Inbox

The outbox is a primary production feature for systems that must change application state and send a message as one reliable operation. MyServiceBus records the outgoing messaging intent in the application's PostgreSQL transaction. A dispatcher can publish the committed record later, without trying to enlist the broker in a distributed transaction.

The matching inbox protects consumer-side database effects from broker redelivery. Its identity record, application changes, and any outgoing outbox messages share one transaction.

## Familiar MassTransit model

MyServiceBus follows MassTransit's useful distinction between a **Bus Outbox** for messages produced in an application scope and a **Consumer Outbox** that combines inbox deduplication with outgoing capture around a consumer. Teams can use [MassTransit's outbox documentation](https://masstransit.massient.com/concepts/outbox) as conceptual background.

MyServiceBus documentation remains authoritative for configuration, supported behavior, and release status. MassTransit configuration snippets are not source-compatible MyServiceBus examples, and MyServiceBus does not claim every ordering, locking, delivery-service, or exactly-once behavior described for a particular MassTransit version.

## Current status

The C# package `Sundstrom.MyServiceBus.PostgreSql` and Java module `io.github.marinasundstrom.myservicebus:myservicebus-postgresql` currently provide:

- an idempotent, versioned PostgreSQL schema installer;
- a caller-transaction-enlisted outbox writer;
- opt-in Bus Outbox capture for scoped `IPublishEndpoint` / `ISendEndpointProvider` and their Java equivalents;
- a message factory that captures the final serialized send context without hand-building persisted envelope metadata;
- shared-storage leasing with `FOR UPDATE SKIP LOCKED`;
- persisted retry, lease, dispatch, and stable message-identity state;
- inbox acquisition and completion using `(consumer scope, message identity)` uniqueness; and
- matching Testcontainers integration tests in C# and Java.

This is a working Bus Outbox capture and persistence foundation, not yet the finished production experience. Hosted dispatch polling, transport adapters for persisted envelopes, transparent Consumer Outbox middleware, retention cleanup, health and metrics, and the full O01–O06 crash matrix remain open. Applications must still compose the store and dispatcher boundary explicitly.

## Why the boundary matters

Writing application state and publishing directly to a broker creates two independent commits. Either operation can succeed while the other fails. The transactional outbox replaces that uncoordinated gap with one database commit:

1. update application state;
2. insert every outgoing message intent into the outbox using the same connection and transaction;
3. commit once; and
4. dispatch committed records to the broker outside the application transaction.

Broker delivery can still be duplicated if a process exits after broker acceptance but before the database records `Dispatched`. MyServiceBus therefore preserves the same message identity across every dispatch attempt. The inbox uses that stable identity to prevent a redelivery from repeating a protected database effect.

## PostgreSQL transaction model

Both clients require a caller-owned transaction. The writer never commits or rolls back it. This makes the transaction boundary visible and prevents an outbox record from accidentally being written in a separate transaction.

Enable the Bus Outbox once during bus registration. Within an application service scope, attach its `OutboxSession` to the active PostgreSQL transaction. Ordinary scoped publish and send endpoints then persist their final filtered envelope instead of contacting the broker. Commit or rollback remains entirely under application control.

### C#

```csharp
services.AddServiceBus(configurator =>
{
    configurator.UseBusOutbox();
    configurator.UsingRabbitMq((_, rabbit) => rabbit.Host("localhost"));
});

await PostgreSqlSchema.EnsureCreatedAsync(dataSource);
await using var scope = serviceProvider.CreateAsyncScope();
await using var connection = await dataSource.OpenConnectionAsync();
await using var transaction = await connection.BeginTransactionAsync();

// Execute the application UPDATE/INSERT with this connection and transaction.

using (scope.ServiceProvider.GetRequiredService<OutboxSession>()
    .UsePostgreSql(connection, transaction))
{
    var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
    await publish.Publish(new OrderSubmitted(orderId), context =>
        context.CorrelationId = orderId.ToString());
}

await transaction.CommitAsync();
```

### Java

```java
services.from(MessageBusServices.class).addServiceBus(configurator -> {
    configurator.useBusOutbox();
    configurator.using(RabbitMqFactoryConfigurator.class,
            (context, rabbit) -> rabbit.host("localhost"));
});

PostgreSqlSchema.ensureCreated(dataSource);
try (ServiceScope scope = serviceProvider.createScope();
     Connection connection = dataSource.getConnection()) {
    connection.setAutoCommit(false);

    // Execute the application UPDATE/INSERT with this connection.

    ServiceProvider scoped = scope.getServiceProvider();
    try (OutboxSession.Registration ignored =
            PostgreSqlOutboxSession.useTransaction(
                    scoped.getRequiredService(OutboxSession.class), connection)) {
        PublishEndpoint publish = scoped.getRequiredService(PublishEndpoint.class);
        publish.publish(new OrderSubmitted(orderId), context ->
                context.setCorrelationId(orderId)).join();
    }

    connection.commit();
}
```

Resolve the scoped endpoint interfaces from the same scope as `OutboxSession`. Calling the singleton `IMessageBus` / `MessageBus` directly intentionally bypasses Bus Outbox capture, matching the familiar distinction between bus-level and scoped endpoint contracts. Nested outbox registrations in one scope are rejected.

Scheduled or delayed messages are not yet supported inside an active outbox session and fail with a clear unsupported-operation exception instead of being delivered early. Scheduling persistence is a separate future slice.

The canonical, maintained usage samples live in the [feature walkthrough](feature-walkthrough.md#transactional-outbox-and-inbox).

## Inbox transaction

Create `PostgreSqlInboxStore` with the same transaction used for the protected application change. Continue only for `Acquired`. Treat `Completed` as a safe duplicate, and leave `InProgress` eligible for retry. For an acquired message, add any responses or publications through `acquisition.Outbox` / `acquisition.getOutbox()`, mark the acquisition complete, and then commit the caller transaction.

The broker source must be settled only after that database commit. A crash after the commit but before broker settlement causes a redelivery; the completed inbox identity makes that redelivery safe.

## Dispatcher ownership

`PostgreSqlOutboxStore` implements the portable leasing contract consumed by `OutboxDispatcher`. Multiple replicas can request batches concurrently. PostgreSQL chooses disjoint rows, records a persisted owner and expiry, and allows another replica to reclaim an expired lease.

Applications still need an `IOutboxTransportDispatcher` / `OutboxTransportDispatcher` implementation that passes the stored envelope to the selected broker transport, plus a hosted polling loop. Those integrations will become framework-owned before the PostgreSQL outbox is presented as a complete production configuration path.

## Guarantees and limits

- Application state and outgoing intent are atomic only when they use the same PostgreSQL transaction.
- A committed outbox record remains recoverable even when the application exits before dispatch.
- Broker delivery is at least once across the broker/database commit gap; it is not globally exactly once.
- Inbox protection applies only to effects committed in its PostgreSQL transaction.
- Calls to external APIs and writes to another database require their own idempotency or coordination strategy.
- The current schema is version 1 and startup fails when it encounters an unsupported schema version.

See the normative [outbox and inbox specification](specs/outbox-inbox.md) and [delivery failure matrix](development/delivery-failure-matrix.md) for the promotion criteria.
