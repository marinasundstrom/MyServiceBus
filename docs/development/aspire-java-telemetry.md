# Aspire Java Telemetry

This repository includes an Aspire AppHost at [`src/AspireApp`](../../src/AspireApp) that starts:

- the C# test app
- the Java test app
- a disposable RabbitMQ broker

## Why the Java app needs extra setup

The AppHost supervises the Java test app through its Gradle `run` task and attaches the OpenTelemetry Java agent for automatic instrumentation.

MyServiceBus already creates spans in both runtimes:

- C# emits activities from the `MyServiceBus` activity source.
- Java emits spans through `GlobalOpenTelemetry`.

Aspire handles OTLP export and the dashboard, but the Java process still needs the OpenTelemetry Java agent JAR to be present on disk.

## Java agent setup

From the repository root:

```bash
mkdir -p src/AspireApp/agents
curl -fL \
  https://github.com/open-telemetry/opentelemetry-java-instrumentation/releases/latest/download/opentelemetry-javaagent.jar \
  -o src/AspireApp/agents/opentelemetry-javaagent.jar
```

The AppHost resolves the agent to an absolute path before passing it to Gradle's forked JVM:

`src/AspireApp/agents/opentelemetry-javaagent.jar`

The Java app also needs a trusted certificate PEM for Aspire's local OTLP endpoint:

`src/AspireApp/agents/aspire-localhost-cert.pem`

The AppHost passes that file to the Java agent through `OTEL_EXPORTER_OTLP_CERTIFICATE`.

See [`src/AspireApp/AppHost.cs`](../../src/AspireApp/AppHost.cs).

## Running locally with Aspire

1. Ensure the Java agent JAR and Aspire localhost certificate PEM exist under `src/AspireApp/agents`.

If the certificate must be refreshed:

```bash
cp /path/to/aspire/cert.pem src/AspireApp/agents/aspire-localhost-cert.pem
```

If the local Aspire certificate rotates, refresh `src/AspireApp/agents/aspire-localhost-cert.pem` with the current PEM before starting the AppHost again.

2. Start the Aspire AppHost from the repository root. It creates RabbitMQ and starts both applications; no separate broker or Java build step is required:

```bash
dotnet run --project src/AspireApp/AspireApp.csproj
```

3. Open the Aspire dashboard URL printed by the AppHost.

## C# telemetry note

The C# app does not need an agent, but it does need to register the custom `MyServiceBus` activity source with OpenTelemetry so Aspire can export those spans. In this repository, `AddServiceDefaults()` now adds that source automatically. Custom hosts still need to call `AddSource("MyServiceBus")` themselves.

See:

- [`src/MyServiceBus/OpenTelemetry.cs`](../../src/MyServiceBus/OpenTelemetry.cs)
- [`src/TestApp/Program.cs`](../../src/TestApp/Program.cs)
