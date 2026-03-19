# Error Handling Strategy

MyServiceBus adopts a unified cross-language approach to reporting and handling failures. The strategy mirrors MassTransit's semantics for transient versus permanent errors while making failure modes more explicit.

## Core principles

- **MassTransit alignment with added clarity**: follow MassTransit's distinction between retryable and non-retryable faults, but validate inputs early and throw well-named exceptions for misconfiguration or illegal arguments.
- **Documented public API**: every public method lists the exceptions it can raise, and only domain-specific or well-understood framework exceptions escape to callers.
- **Platform idioms**: C# surfaces errors via typed exceptions and XML documentation for notable public exceptions, while Java uses `throws` clauses when callers are expected to recover.

## Exception taxonomy

- Introduce a shared base `MyServiceBusException`.
- Derive specific exceptions for transport, serialization, authentication/authorization, timeout, and user-operation failures.
- Wrap underlying framework or I/O errors to preserve stack traces.

## API surface lockdown

- Restrict helpers not meant for consumption (`internal`/`sealed` in C#, `package-private`/`final` in Java).
- Maintain parity so that non-public APIs remain hidden in both languages.

## Implementation guidelines

- Validate arguments at method entry, throwing `ArgumentException`/`IllegalArgumentException` as appropriate.
- Catch low-level exceptions, translate to domain-specific types, and rethrow.
- Preserve cancellation semantics (`OperationCanceledException`/`InterruptedException`).
- For asynchronous APIs, propagate errors through the returned `Task`/`CompletableFuture`.

## Documentation and testing

- XML documentation and Javadoc list each possible exception.
- Examples in [`feature-walkthrough.md`](feature-walkthrough.md) illustrate recovery patterns.
- Run `dotnet test` and `./gradlew test` to verify behavior across languages.

## Cross-language parity

- C# does not emulate Java's compile-time exception declarations. Catch and translate exceptions where it improves the API, and document notable public exceptions in XML docs.
- Java relies on language-level exception declarations. Use them when callers can act on the failure and runtime exceptions for programming errors.
