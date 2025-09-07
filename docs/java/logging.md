# Logging

The Java project defines its own minimal logging abstraction so applications
can choose their preferred backend.

## Interfaces

- `Logger` – provides `debug`, `info`, `warn`, and `error` methods along with
  `isDebugEnabled` for conditional logging.
- `LoggerFactory` – creates loggers either by class or by name.

## Default provider

By default, MyServiceBus supplies `Slf4jLoggerFactory`, which delegates to
SLF4J and produces `Slf4jLogger` instances that wrap `org.slf4j.Logger`.
This allows any SLF4J-compatible implementation (Logback, Log4j, etc.) to
serve as the logging backend.

## Console logger

For simple scenarios, the `ConsoleLoggerFactory` writes log messages directly
to `System.out`/`System.err`. Logging can be filtered globally or per
category using `ConsoleLoggerConfig`:

```java
ServiceCollection services = new ServiceCollection();
services.addConsoleLogger(cfg -> {
    cfg.setMinimumLevel(LogLevel.WARN);
    cfg.setLevel("com.myservicebus", LogLevel.DEBUG);
});
```

With the above configuration, only warnings and errors are logged by default,
while classes under the `com.myservicebus` package log at the debug level.
