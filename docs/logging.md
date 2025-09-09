# Logging

MyServiceBus uses structured logging to capture key events in the bus.
The .NET implementation is built on `Microsoft.Extensions.Logging`,
which allows applications to plug in any provider. In Java, logging
providers are registered through a `Logging` decorator:

```java
ServiceCollection services = new ServiceCollection();
services.for(Logging.class)
        .addLogging(b -> b.addConsole());
```

If no provider is configured, a console logger is added automatically.

## What is logged

- **Message operations** – sending, publishing, and receiving messages log
  at the `Debug` level for traceability of message flow (message type and destination).
- **Lifecycle events** – starting and stopping the bus is reported at the
  `Information` level.
- **Recoverable issues** – conditions such as malformed headers or
  unregistered message types emit `Warning` logs.
- **Faults** – handler or consumer exceptions are written at the `Error`
  level before being rethrown.

These categories provide a consistent logging story across transports and
make it easier to correlate events when diagnosing issues.

For details on the Java logging abstraction, see
[`docs/java/logging.md`](java/logging.md).
