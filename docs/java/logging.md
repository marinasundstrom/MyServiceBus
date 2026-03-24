# Logging

The Java project defines its own minimal logging abstraction so applications can choose their preferred backend.

## Interfaces

- `Logger` – provides `debug`, `info`, `warn`, and `error` methods along with `isDebugEnabled` for conditional logging.
- `LoggerFactory` – creates loggers either by class or by name.

## Configuration

Logging providers are registered through a `Logging` decorator that exposes a `LoggingBuilder`:

```java
ServiceCollection services = ServiceCollection.create();
services.from(Logging.class)
        .addLogging(builder -> builder.addConsole());
```

If no provider is configured, a console logger is added automatically.

The console logger can be tuned with `ConsoleLoggerConfig`:

```java
services.from(Logging.class)
        .addLogging(builder -> builder.addConsole(cfg -> {
            cfg.setMinimumLevel(LogLevel.WARN);
            cfg.setLevel("com.myservicebus", LogLevel.DEBUG);
        }));
```

SLF4J can also be configured via `Slf4jLoggerConfig`:

```java
services.from(Logging.class)
        .addLogging(builder -> builder.addSlf4j(cfg -> {
            cfg.setMinimumLevel(LogLevel.WARN);
            cfg.setLevel("com.myservicebus", LogLevel.DEBUG);
        }));
```

If no providers are registered, `ConsoleLoggerFactory` is added automatically.

## Factory creation

A `LoggerFactory` can be created outside dependency injection:

```java
LoggerFactory factory = LoggerFactoryBuilder.create(b -> b.addConsole());
```

## Runtime behavior

- Message flow logs follow the same shape as the C# client: `Sending ... to ...`, `Publishing ... to ...`, and `Received ...` are emitted at `DEBUG`.
- Bus lifecycle events are emitted at `INFO` when the bus starts and stops.
- Warnings are used for recoverable conditions such as unregistered message types, and faults are logged at `ERROR`.

This keeps the Java client close to the MassTransit logging model while still using Java-native logging backends.
