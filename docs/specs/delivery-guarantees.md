# Delivery Guarantees Specification

## Status and Scope

This document records the current preview behavior and the production target for broker-backed delivery. It covers the C# and Java clients on RabbitMQ and Azure Service Bus.

The current-behavior sections describe what the implementations do today; they are not stronger promises than the versioned [compatibility policy](../compatibility.md). The target requirements become release guarantees only after the corresponding scenarios in the [delivery failure matrix](../development/delivery-failure-matrix.md) pass for a named release.

## Terms

- **Application completion**: the send, publish, or request API returns successfully to its caller.
- **Broker acceptance**: the broker has positively acknowledged accepting a message under the configured durability mode.
- **Routing acceptance**: a directed message reached at least one intended broker destination. Publishing an event with no current subscriber is valid and does not require a routed queue.
- **Delivery**: one broker attempt made available to a receiver under a broker lock or acknowledgement token.
- **Application success**: every consumer pipeline selected for that delivery completed successfully.
- **Terminal failure**: retries were exhausted or a non-retryable pipeline stage failed.
- **Settlement**: the source delivery was acknowledged, completed, abandoned, rejected, or allowed to become available again.
- **Ambiguous outcome**: the caller cannot determine whether the broker committed an operation, usually because the connection failed between broker acceptance and client acknowledgement.

## Portable Production Contract

MyServiceBus does not promise exactly-once delivery or exactly-once application effects. The production target is an at-least-once processing model with explicit exceptions for application-requested expiration and administrative broker actions.

The portable contract requires:

1. A successful directed send means broker acceptance and routing acceptance. An unroutable directed send fails visibly.
2. A successful publish means broker acceptance. Zero subscribers is a valid publish outcome.
3. An ambiguous send or publish outcome fails visibly and is safe for an idempotent caller to retry with the same message identity.
4. A source delivery is settled as successful only after its application pipeline succeeds.
5. A terminally failed or skipped source delivery is settled only after its compatibility copy is broker-accepted by `_error` or `_skipped` respectively.
6. Failure to create the required compatibility copy leaves the source delivery eligible for redelivery.
7. A process failure before successful source settlement leaves the source delivery eligible for redelivery.
8. Retries re-run application code. Consumers must assume an earlier attempt performed partial effects before failing.
9. Message identity remains stable across broker redelivery, in-process retries, and compatibility copies.
10. Cancellation stops waiting where the transport supports it; it does not prove that a broker operation or remote consumer was cancelled.

These requirements prevent known message-loss windows, but they do not make application database changes atomic with broker operations. The outbox and inbox/idempotency work described below is required for that boundary.

## Producer Operations

### Directed Send

The target completion point is broker and routing acceptance. A connection failure, negative acknowledgement, returned unroutable message, authorization failure, missing destination, or expired caller deadline must fail the operation.

If the broker may have accepted the message but the client did not receive the acknowledgement, the operation has an ambiguous outcome. MyServiceBus must surface the failure and retain the original message identity when an application or outbox retries it. The runtime must not claim that the message was rejected.

### Publish

The target completion point is broker acceptance by the named publish entity. Publish does not require a current subscription: an event may legitimately have zero consumers. Failure to reach or receive acceptance from the broker fails the operation.

### Current RabbitMQ Preview

Both clients declare durable entities and mark ordinary messages persistent by default. Durable entities and persistent messages are necessary but insufficient for confirmed delivery.

- C# enables publisher confirmations and client-side confirmation tracking on transport channels. `BasicPublishAsync` completes after the tracked acknowledgement or fails on a negative acknowledgement.
- Java selects confirmation mode and waits for the broker acknowledgement after each transport publish.
- Directed sends through `queue:` addresses use mandatory routing. Java's dedicated RabbitMQ request transport also requires routing.
- Event publication keeps mandatory routing disabled because publishing with no current subscriber is valid.
- Skipped-message copies require both mandatory routing and publisher confirmation before the source is acknowledged.
- Error, fault, and skipped compatibility exchanges require mandatory routing as well as publisher confirmation. Their companion bindings are provisioned before receive starts; deletion or mutation of that topology therefore fails the compatibility send instead of silently accepting an unrouted copy.
- A connection failure can produce either a visible failure or an ambiguous outcome. Automatic connection recovery does not retroactively confirm or replay an application publish.

This behavior establishes the confirmation foundation but is not yet the complete portable production target. Stable retry identity, connection-ambiguity tests, and process-level failure tests remain release blockers for the RabbitMQ production profile.

### Current Azure Service Bus Preview

Both clients complete a send after the Azure SDK's send call returns. This provides service acceptance for the queue or topic under the SDK contract. Authorization, missing-entity, and service failures surface as transport exceptions.

A timeout or connection loss around service acceptance can still be ambiguous: the service may have accepted the message before the client observed failure. Retrying can duplicate the message. Broker duplicate detection is not exposed by the current profile, so application or inbox idempotency remains required.

## Consumer Operations

### Successful Consumption

The target settlement point is after all selected consumer pipelines complete. If the process exits after application effects but before settlement, the broker may redeliver the message. This is expected at-least-once behavior.

Multiple consumers attached to one logical endpoint are part of one source-delivery outcome. If any selected consumer fails, the endpoint delivery is not application-successful even if another consumer has already performed effects.

### In-Process Retry

The current retry filters run the same pipeline again before the source delivery is settled. An immediate retry starts without broker redelivery; an interval retry waits in the process. Azure Service Bus retains and renews the delivery lock through supported long-running handling. RabbitMQ retains the unacknowledged delivery.

No rollback occurs between attempts. A handler can perform an external effect and then throw, so every attempt and any later broker redelivery may repeat that effect.

Scheduled or delayed in-process retries are not durable across process termination. A process exit releases the broker delivery for broker redelivery; it does not preserve the remaining in-process delay.

### Terminal Failure and Fault Publication

After retries are exhausted, the pipeline attempts to publish a compatible `Fault<T>` and copy the failed message to `_error`. Fault publication is a notification and may itself fail. Preservation of the original failed message in `_error` takes precedence over a fault notification.

The target sequence is:

1. Complete all configured in-process attempts.
2. Attempt the correlated or endpoint fault publication.
3. Copy the original message and failure metadata to `_error` with the same message identity.
4. Receive broker acceptance for the `_error` copy.
5. Settle the source delivery.

If steps 2 through 4 fail, the source remains eligible for redelivery. An ambiguous `_error` acceptance can create duplicate error copies when the source is retried. Error consumers must therefore be idempotent.

### Skipped Messages

An unrecognized message is copied unchanged to `_skipped`. The source is settled only after the copy is broker-accepted. An ambiguous copy can result in duplicate skipped messages; it must not result in silently losing the only copy.

### Current RabbitMQ Preview

RabbitMQ deliveries use manual acknowledgement and distinguish confirmed terminal preservation from other receive failures.

- Success is acknowledged after the handler returns.
- A process exit before acknowledgement allows RabbitMQ to redeliver the source.
- `_error` sends are publisher-confirmed before the failure is marked as moved. `_skipped` copies are publisher-confirmed and require routing.
- The source is acknowledged after success or after a failure was marked as moved to `_error`.
- Other receive-path failures are negatively acknowledged with requeue enabled.
- A crash after RabbitMQ accepted a compatibility copy but before the source acknowledgement can produce a duplicate compatibility copy after redelivery.

This behavior closes the known unconfirmed-copy acknowledgement window and requires routing for the compatibility exchanges. Production promotion still requires real-broker injection at every confirmation and settlement boundary, poison-message policy for repeatedly malformed input, and duplicate-identity verification.

### Current Azure Service Bus Preview

Azure receivers use peek-lock with automatic completion disabled and a configurable portable concurrent-message limit, defaulting to one, mapped to the processor's native maximum-concurrent-calls option.

- Success completes the source after the handler returns.
- A skipped message is sent to `_skipped` before the source is completed.
- A terminal failure marked as moved by the error filter completes the source.
- Other failures abandon the source; failure to abandon leaves lock expiry as the fallback.
- A process exit or cancellation before completion releases the lock for redelivery.
- The `_error` or `_skipped` send and source completion are not one atomic transaction. Acceptance followed by an ambiguous client failure can create duplicate compatibility copies.

This is an at-least-once preservation model, subject to broker expiration, maximum-delivery, and administrative policies. The failure matrix must prove that a failed copy never causes source completion.

## Consumer-Initiated Send and Publish

Sending or publishing from a consumer is not atomic with application state or source settlement.

| Failure point | Possible outcome without an outbox |
| --- | --- |
| Application state commits, outgoing message fails | State exists without the outgoing message |
| Outgoing message succeeds, application state rolls back | Message exists for state that did not commit |
| Outgoing message succeeds, process exits before source settlement | Source is redelivered and the outgoing message may be sent again |
| Source settles, process exits before unrelated state commits | Message is consumed but the intended state may be absent |

A transactional outbox must store application state and outgoing message intent in one supported database transaction, then dispatch the message independently with stable identity. An inbox or idempotent-consumer store must record completed message identities atomically with consumer state where the persistence technology permits it.

Until supported outbox/inbox integrations ship, applications own these patterns. Documentation and samples must not imply atomic consume-and-produce behavior.

## Request and Response

Request/response is composed from a request send, a temporary response endpoint, correlation identifiers, and a client-side timeout.

- The response endpoint starts before the request is sent.
- Successful API completion means that a correlated response was received and decoded.
- A request timeout or caller cancellation stops the client's wait and removes or stops the temporary endpoint. It does not cancel a request already accepted by the broker or work already started by a consumer.
- A late response can be discarded because the temporary endpoint no longer exists.
- A consumer may execute more than once, and responses or faults may be duplicated.
- Request handlers that perform effects must be idempotent independently of the request client's timeout.

RabbitMQ response endpoints are non-durable and auto-delete. Azure response queues are temporary and auto-delete in `Create` mode or explicitly mapped in `PreProvisioned` mode. They are not durable workflow state.

## Scheduling

Portable scheduling is currently emulated by an in-process job scheduler. Scheduling API completion does not mean a broker has durably stored the message for future delivery. Process termination can lose pending schedules.

Production documentation must label this mode non-durable. A transport may claim durable scheduling only after using a broker-native or persistent scheduler and passing restart, cancellation, duplicate, and clock-boundary tests.

## Shutdown and Flow Control

Prefetch limits broker deliveries that have not been acknowledged. The portable concurrent-message limit independently bounds application handler execution, but it is not yet a complete overload policy because callback-queue bounds and saturation evidence remain open.

The production target requires:

- stop accepting new deliveries before draining existing work
- a configurable drain deadline
- source deliveries left unsettled when the deadline forces termination
- no acknowledgement after a delivery has been released or its channel closed
- saturation evidence for endpoint concurrency independent of prefetch
- bounded queues between broker callbacks and application handlers

Both clients expose an explicit timed stop (`StopAsync(TimeSpan, ...)` in C# and `stop(Duration)` in Java) and report expiry as `BusStopTimeoutException`. The timeout is shared across receive transports rather than restarted for every endpoint.

RabbitMQ C# and Java cancel the consumer before waiting for active callbacks, including deliveries waiting for a concurrency permit, to settle. On expiry they abort the receive channel, so unsettled sources remain eligible for broker redelivery and later handler completion cannot acknowledge them on that channel. Azure stops its processor; on expiry the clients initiate processor and sender teardown so active locks are not completed as successful work.

These are runtime guarantees for the built-in transports. A third-party C# transport must observe the supplied cancellation token, and a third-party Java transport must override the timed `ReceiveTransport.stop(Duration)` method, before it can claim bounded shutdown. Real-broker deadline, redelivery, and settlement-race evidence remains required before the matrix can mark forced stop verified.

## Ordering, Duplication, and Expiration

- Ordering is scoped to the broker entity and can be changed by concurrent processing, retry, redelivery, multiple consumers, and compatibility copies.
- No global ordering is promised.
- Duplicate delivery is expected after ambiguous producer outcomes, consumer crashes before settlement, and compatibility-copy settlement races.
- Message expiration and broker retention policies can intentionally prevent later delivery and are outside an unconditional at-least-once claim.
- Administrative purge, deletion, dead-letter limits, or topology changes can remove messages independently of the runtime.

Applications that require ordered processing must select and document a transport-specific ordering mechanism. Azure sessions and equivalent partitioning features are not part of the current portable profile.

## Promotion Requirements

A transport profile may be promoted to production-ready only when:

- its producer completion point is verified under success, rejection, unroutable delivery, cancellation, timeout, and connection ambiguity
- every receive exit path has an explicit settlement outcome
- failed and skipped preservation is confirmed before source settlement
- process and broker interruption tests show redelivery without unexplained loss
- duplicate windows are documented and carry stable message identity
- shutdown and overload behavior pass the shared failure matrix
- C# and Java produce the same application-visible outcomes for portable operations

The executable scenarios and current coverage are tracked in the [Delivery Failure Matrix](../development/delivery-failure-matrix.md).
