# Changelog

This changelog summarizes the bigger themes in the repository history. It is intentionally thematic rather than exhaustive, and is based on work landed between April 4, 2025 and March 24, 2026.

## 2026-03-24 to 2026-03-19

### Aspire, runtime modernization, and parity cleanup

- Added Aspire-based local orchestration work, including RabbitMQ configuration and shared service defaults.
- Upgraded the .NET stack to .NET 10.
- Reworked telemetry setup and brought logging behavior closer to parity between C# and Java.
- Refactored message conventions and serialization, including fixes for raw message serialization.
- Updated sample scenarios to keep the C# and Java experiences aligned.
- Removed use of the CheckedExceptions analyzer as the Java implementation matured.

## 2025-12-16 to 2025-10-30

### Developer experience and hosting direction

- Added `AspireApp`, establishing the direction for orchestrated local development.
- Expanded top-level documentation, including README updates, Java FAQ material, and feature walkthrough improvements.
- Continued tightening the onboarding story for both languages without introducing a large new feature wave.

## 2025-09-10 to 2025-09-09

### Scheduling, bus factory cleanup, and logging ergonomics

- Added message scheduling support and follow-up test tolerance fixes in Java.
- Refined the bus factory surface, including a more self-contained factory model and Java API alignment.
- Renamed `IScopedClientFactory` to `IRequestClientFactory` to better reflect intent.
- Added consumer factory configuration and improved service collection integration patterns.
- Introduced default console logging across the bus and added Java logging builder support.
- Moved the Gradle build to the repository root and improved related documentation.

## 2025-09-08 to 2025-09-07

### Reliability, topology, formatting, and observability

- Strengthened RabbitMQ failure handling with fault queues, skipped queues, requeue behavior, delivery acknowledgement fixes, and health checks.
- Replaced naming conventions with formatter-based customization, including message entity name formatters.
- Added per-endpoint serializer configuration and anonymous message support in Java.
- Allowed multiple consumers per queue or per message type and tightened duplicate registration handling.
- Added OpenTelemetry instrumentation and expanded the logging abstractions and documentation.
- Added and refined architecture, pipeline, exception-handling, and portability documentation to support the larger design direction.

## 2025-09-06 to 2025-09-05

### Build, testing, documentation, and cross-language governance

- Migrated the Java build to Gradle and then improved Gradle usage, properties, and repository layout.
- Added CI workflows for .NET and Java, plus markdown-only workflow skipping and manual workflow dispatch support.
- Formalized project guidance around cross-language parity, testing expectations, API visibility, and error-handling strategy.
- Expanded the documentation set substantially with development guides, architecture notes, migration guidance, parity checklists, and testing guidance.
- Added OpenTelemetry instrumentation and made retries opt-in across C# and Java.
- Improved serializer, header, error transport, and envelope behavior to better match MassTransit semantics.

## 2025-09-04 to 2025-09-02

### Request/response, DI, transport abstraction, and test harnesses

- Added scoped request client factories in both C# and Java and expanded request/response handling, including fault responses and temporary reply endpoints.
- Added RabbitMQ bus configurators and refactored transport-specific configuration around a more transport-agnostic core.
- Built out dependency injection support, including scoped services, send endpoint providers, Guice integration, and host/container-based registration.
- Added in-memory mediator transports and test harnesses for both .NET and Java, with broader consumer and fault-handling coverage.
- Introduced pipeline filters, retry support, configurable serializers, and topology registry work in Java.
- Expanded quick-start and feature walkthrough material to keep the new abstractions usable.

## 2025-09-01

### First major parity push

- Established the initial cross-language structure for C# and Java under a shared MyServiceBus model.
- Added in-memory mediator transports, endpoint configuration, fault handling, retry filters, batch support, and consumer startup behavior.
- Documented shared messaging concepts, Java capabilities, exception-handling expectations, and repository contributor instructions.
- Added AGENTS guidance for the repository and for the Java subtree to reinforce parity and documentation discipline.

## 2025-07-24 to 2025-07-18

### Analyzer and dependency maintenance

- Updated the CheckedExceptions-related packages and settings during the period when checked-exception guidance was still part of the Java workflow.
- Added unit tests for dependency injection and reorganized supporting code.

## 2025-04-13 to 2025-04-04

### Project foundation and first usable clients

- Created the repository, initial .NET solution, and Java projects.
- Established the core messaging API shape, topology concepts, and host configuration direction.
- Built the first working C# and Java client prototypes.
- Added early Java dependency injection work, consumer handling, and scoped dependency fixes.
- Introduced request/response support in C#, including `GenericRequestClient`, cancellation, timeout handling, and request client tests.
- Added initial README and setup documentation to make the project runnable.

## Maintenance policy

Keep this file updated for significant changes. Prefer adding dated entries that summarize the main themes of a change set instead of listing every commit.
# Unreleased

- Added sample-app dashboard endpoints in the .NET and Java `TestApp` projects under `/dashboard/v1/*`, exposing stable JSON snapshots for bus overview, messages, consumers, and topology without committing those contracts to the shared libraries yet.
