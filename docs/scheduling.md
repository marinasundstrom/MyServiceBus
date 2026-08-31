# Message Scheduling

MyServiceBus separates **volatile in-process scheduling** from **durable message scheduling**. Both C# and Java expose the same publish, directed-send, handle, and cancellation concepts, but the selected provider determines the actual guarantee.

## Choose the guarantee first

| Mode | Persistence | Process restart | Cancellation | Current status |
| --- | --- | --- | --- | --- |
| Default in-memory provider | Process memory | Pending work is lost | Supported before the callback starts | Available in C# and Java |
| PostgreSQL outbox scheduler | Caller-owned PostgreSQL transaction | Persisted intent remains eligible after restart | Persisted, with an explicit race result | Available for evaluation in C# and Java |
| Custom message-aware provider | Provider-defined | Provider-defined and reported as durable or volatile | Provider-defined | Extension point available |
| Broker-native, Quartz.NET, Quartz Scheduler, Hangfire, JobRunr, or recurring adapters | Provider-defined | Not claimed until separately implemented and tested | Provider-defined | Future adapters |

Scheduling a callback and scheduling a message are different extension points. `IJobScheduler` / `JobScheduler` receives an executable callback and is therefore suitable only for process-bound timing and deterministic tests. `IScheduleMessageProvider` / `ScheduleMessageProvider` receives the message delivery intent and is the integration boundary for an implementation that serializes and persists work outside the process.

## Use the familiar message scheduler

The C# surface follows the current MassTransit shape where it helps migration: scheduled time precedes the message for the primary absolute-time overload, publish and send remain distinct, and a handle carries the cancellation token. Java preserves the same concepts with `Instant`, `Duration`, `CompletionStage`, and Java naming.

### C#

```csharp
var scheduler = scope.ServiceProvider.GetRequiredService<IMessageScheduler>();
var dueAt = DateTime.UtcNow.AddMinutes(5);

ScheduledMessageHandle publish = await scheduler.SchedulePublish(
    dueAt,
    new PaymentReminder(orderId));

ScheduledMessageHandle send = await scheduler.ScheduleSend(
    new Uri("queue:billing"),
    dueAt,
    new CollectPayment(orderId));

if (scheduler.SupportsCancellation)
    await scheduler.CancelScheduledPublish(publish);
```

### Java

```java
MessageScheduler scheduler = scope.getRequiredService(MessageScheduler.class);
Instant dueAt = Instant.now().plus(Duration.ofMinutes(5));

ScheduledMessageHandle publish = scheduler
    .schedulePublish(dueAt, new PaymentReminder(orderId))
    .toCompletableFuture().join();

ScheduledMessageHandle send = scheduler
    .scheduleSend("queue:billing", dueAt, new CollectPayment(orderId))
    .toCompletableFuture().join();

if (scheduler.supportsCancellation()) {
    scheduler.cancelScheduledPublish(publish).toCompletableFuture().join();
}
```

The default provider reports `Volatile` / `VOLATILE`. API completion means the process accepted the timer, not that a broker or database durably accepted the message.

## Commit delayed intent through the outbox

Inside an active PostgreSQL Bus Outbox session, set `ScheduledEnqueueTime` on the ordinary scoped publish or send endpoint. MyServiceBus stores the final serialized envelope and its due time in the application transaction. The dispatcher cannot lease it before that time.

```csharp
using (outboxSession.UsePostgreSql(connection, transaction, "orders-service"))
{
    await publishEndpoint.Publish(new PaymentReminder(orderId), context =>
        context.SetScheduledEnqueueTime(DateTime.UtcNow.AddHours(1)));
}

await transaction.CommitAsync();
```

```java
try (OutboxSession.Registration ignored = PostgreSqlOutboxSession.useTransaction(
        outboxSession, connection, "orders-service")) {
    publishEndpoint.publish(new PaymentReminder(orderId), context ->
        context.setScheduledEnqueueTime(Instant.now().plus(Duration.ofHours(1)))).join();
}

connection.commit();
```

For the typed scheduler API, register the PostgreSQL provider and schedule while the same outbox transaction is active:

```csharp
services.AddSingleton(dataSource);
services.AddPostgreSqlMessageScheduler("orders-service");

ScheduledMessageHandle handle;
using (outboxSession.UsePostgreSql(connection, transaction, "orders-service"))
{
    handle = await scheduler.SchedulePublish(dueAt, new PaymentReminder(orderId));
}
await transaction.CommitAsync();

ScheduleCancellationResult result = await scheduler.CancelScheduledPublish(handle);
```

```java
PostgreSqlScheduling.addMessageScheduler(services, dataSource, "orders-service");

ScheduledMessageHandle handle;
try (OutboxSession.Registration ignored = PostgreSqlOutboxSession.useTransaction(
        outboxSession, connection, "orders-service")) {
    handle = scheduler.schedulePublish(dueAt, new PaymentReminder(orderId))
            .toCompletableFuture().join();
}
connection.commit();

ScheduleCancellationResult result = scheduler.cancelScheduledPublish(handle)
        .toCompletableFuture().join();
```

The handle token is the persisted message identity. Cancel after the producing transaction commits. Cancellation and dispatcher leasing compete through one conditional state transition: exactly one wins. Results distinguish `Cancelled`, `AlreadyCancelled`, `TooLate`, `NotScheduled`, and `NotFound` (uppercase enum names in Java). A cancelled row remains as operational history and cannot be leased. Retry after a failed or ambiguous dispatch reuses the same record and message identities.

## Add a durable provider

Register a message-aware provider before the default registration. A Quartz.NET adapter is a natural .NET implementation; a Java adapter may use Quartz Scheduler or another durable service. The adapter must store a serializable message command or final envelope. Persisting a delegate, lambda, or closure is not a valid durable implementation.

```csharp
services.AddScoped<IScheduleMessageProvider, QuartzScheduleMessageProvider>();
services.AddServiceBus(configurator =>
{
    // transport and consumers
});
```

```java
MessageBusServices busServices = services.from(MessageBusServices.class);
busServices.tryAddScoped(ScheduleMessageProvider.class,
    provider -> () -> new QuartzScheduleMessageProvider(/* application dependencies */));
busServices.addServiceBus(configurator -> {
    // transport and consumers
});
```

A provider that reports `Durable` / `DURABLE` must prove persisted acceptance, restart recovery, stable identity, due-time boundaries, cancellation races, and ambiguous dispatch behavior. Recurring schedules are a separate capability and are not part of the current interface.

Provider support in C# and Java means compatible MyServiceBus APIs and behavior; it does not require the applications to use the same scheduler engine. Hangfire may be appropriate for a .NET application and JobRunr for a Java application. Their records are not portable between engines, but both can export normalized scheduling state to monitoring. Use the MyServiceBus PostgreSQL scheduler when C# and Java applications need to share the same persisted scheduled-message records and envelopes.

## Relationship to MassTransit

MassTransit distinguishes transport-based scheduling from scheduler services such as Quartz.NET or Hangfire. MyServiceBus preserves that useful model and the familiar `IMessageScheduler` vocabulary. It deliberately adds an explicit durability capability and defines a matching Java seam. It does not currently claim MassTransit scheduler breadth, recurring scheduling, broker-native scheduling, or source-compatible provider implementations.

MassTransit's current [message scheduler documentation](https://masstransit.io/documentation/configuration/scheduling) is useful conceptual background. MyServiceBus documentation and tests remain authoritative for the capabilities above.
