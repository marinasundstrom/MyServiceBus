# Transactional Outbox and Inbox

The outbox is a primary production feature for systems that must change application state and send a message as one reliable operation. MyServiceBus records the outgoing messaging intent in the application's PostgreSQL transaction. A dispatcher can publish the committed record later, without trying to enlist the broker in a distributed transaction.

The matching inbox protects consumer-side database effects from broker redelivery. Its identity record, application changes, and any outgoing outbox messages share one transaction.

## Current status

The C# package `Sundstrom.MyServiceBus.PostgreSql` and Java module `io.github.marinasundstrom.myservicebus:myservicebus-postgresql` currently provide:

- an idempotent, versioned PostgreSQL schema installer;
- a caller-transaction-enlisted outbox writer;
- shared-storage leasing with `FOR UPDATE SKIP LOCKED`;
- persisted retry, lease, dispatch, and stable message-identity state;
- inbox acquisition and completion using `(consumer scope, message identity)` uniqueness; and
- matching Testcontainers integration tests in C# and Java.

This is the working persistence foundation, not yet the finished high-level bus experience. Transparent `Send`/`Publish` capture, hosted dispatch polling, transport adapters for persisted envelopes, retention cleanup, health and metrics, and the full O01–O06 crash matrix remain open. Until those layers land, applications must compose the writer, store, transaction, and dispatcher boundary explicitly.

## Why the boundary matters

Writing application state and publishing directly to a broker creates two independent commits. Either operation can succeed while the other fails. The transactional outbox replaces that uncoordinated gap with one database commit:

1. update application state;
2. insert every outgoing message intent into the outbox using the same connection and transaction;
3. commit once; and
4. dispatch committed records to the broker outside the application transaction.

Broker delivery can still be duplicated if a process exits after broker acceptance but before the database records `Dispatched`. MyServiceBus therefore preserves the same message identity across every dispatch attempt. The inbox uses that stable identity to prevent a redelivery from repeating a protected database effect.

## PostgreSQL transaction model

Both clients require a caller-owned transaction. The writer never commits or rolls back it. This makes the transaction boundary visible and prevents an outbox record from accidentally being written in a separate transaction.

### C#

```csharp
await PostgreSqlSchema.EnsureCreatedAsync(dataSource);

await using var connection = await dataSource.OpenConnectionAsync();
await using var transaction = await connection.BeginTransactionAsync();

// Execute the application UPDATE/INSERT with this connection and transaction.

var outbox = new PostgreSqlOutboxWriter(connection, transaction);
await outbox.AddAsync(new OutboxMessage(
    recordId: Guid.NewGuid(),
    messageId: Guid.NewGuid(),
    intent: OutboxDeliveryIntent.Publish,
    destinationAddress: new Uri("rabbitmq://broker/order-submitted"),
    messageTypes: ["urn:message:Contracts:OrderSubmitted"],
    body: serializedEnvelope,
    contentType: "application/vnd.masstransit+json",
    headers: headers,
    createdAtUtc: DateTimeOffset.UtcNow,
    correlationId: orderId));

await transaction.CommitAsync();
```

### Java

```java
PostgreSqlSchema.ensureCreated(dataSource);

try (Connection connection = dataSource.getConnection()) {
    connection.setAutoCommit(false);

    // Execute the application UPDATE/INSERT with this connection.

    OutboxWriter outbox = new PostgreSqlOutboxWriter(connection);
    outbox.add(new OutboxMessage(
            UUID.randomUUID(),
            UUID.randomUUID(),
            OutboxDeliveryIntent.PUBLISH,
            URI.create("rabbitmq://broker/order-submitted"),
            List.of("urn:message:Contracts:OrderSubmitted"),
            serializedEnvelope,
            "application/vnd.masstransit+json",
            headers,
            Instant.now(),
            null,
            orderId,
            null,
            null,
            null,
            null), CancellationToken.none()).join();

    connection.commit();
}
```

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
