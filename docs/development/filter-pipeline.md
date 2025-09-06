# Filter Pipeline

MyServiceBus composes message handling using a pipe-and-filter architecture. The built-in filters address common cross-cutting concerns:

- **ErrorTransportFilter** – captures unhandled exceptions and moves the message to the endpoint's `<queue>_error` transport.
- **ConsumerFaultFilter** – publishes a `Fault<T>` to the fault or response address and logs the failure.
- **RetryFilter** – retries the downstream pipe a configured number of times.
- **ConsumerMessageFilter** – resolves the scoped consumer and invokes its `Consume` method.

For consumer pipelines, filters are applied in the following order:

1. `ErrorTransportFilter`
2. `ConsumerFaultFilter`
3. `RetryFilter`
4. `ConsumerMessageFilter`

This ordering ensures that retries and consumer logic execute within fault notification and error-handling scopes.
