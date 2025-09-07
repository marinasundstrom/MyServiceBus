# Filter Pipeline

MyServiceBus composes message handling using a pipe-and-filter architecture. The built-in filters address common cross-cutting concerns:

- **ErrorTransportFilter** – captures unhandled exceptions and moves the message to the endpoint's `<queue>_error` transport.
- **ConsumerFaultFilter** – publishes a `Fault<T>` to the fault or response address and logs the failure.
- **RetryFilter** – retries the downstream pipe a configured number of times.
- **ConsumerMessageFilter** – resolves the scoped consumer and invokes its `Consume` method.

## Transport Integration

The same pipeline model wraps every transport:

### Send/Publish

1. Framework filters (for example `OpenTelemetrySendFilter`) decorate the outbound context.
2. A transport-specific filter delivers the serialized envelope.
   - The in-memory mediator dispatches directly to the consumer pipeline.
   - The RabbitMQ transport publishes to the broker.

### Receive

Transports feed incoming envelopes into a consumer pipeline where the built-in filters run in order:

1. `ErrorTransportFilter`
2. `ConsumerFaultFilter`
3. `RetryFilter`
4. `ConsumerMessageFilter`

This ordering ensures that retries and consumer logic execute within fault notification and error-handling scopes.
