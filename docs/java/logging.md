# Logging

The Java project defines its own minimal logging abstraction so applications can choose their preferred backend.

## Interfaces

- `Logger` – provides `debug`, `info`, `warn`, and `error` methods along with `isDebugEnabled` for conditional logging.
- `LoggerFactory` – creates loggers either by class or by name.

## Configuration

Logging providers are registered through a `Logging` decorator that exposes a `LoggingBuilder`:

```java
ServiceCollection services = new ServiceCollection();
services.for(Logging.class)
        .addLogging(builder -> builder.addConsole());
```

The console logger can be tuned with `ConsoleLoggerConfig`:

```java
services.for(Logging.class)
        .addLogging(builder -> builder.addConsole(cfg -> {
            cfg.setMinimumLevel(LogLevel.WARN);
            cfg.setLevel("com.myservicebus", LogLevel.DEBUG);
        }));
```

SLF4J can also be configured via `Slf4jLoggerConfig`:

```java
services.for(Logging.class)
        .addLogging(builder -> builder.addSlf4j(cfg -> {
            cfg.setMinimumLevel(LogLevel.WARN);
            cfg.setLevel("com.myservicebus", LogLevel.DEBUG);
        }));
```

If no providers are registered, `Slf4jLoggerFactory` is added automatically so any SLF4J-compatible implementation (Logback, Log4j, etc.) can serve as the logging backend.

## Factory creation

A `LoggerFactory` can be created outside dependency injection:

```java
LoggerFactory factory = LoggerFactoryBuilder.create(b -> b.addConsole());
```
