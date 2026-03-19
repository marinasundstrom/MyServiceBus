# Aspire Java Telemetry

This repository includes an Aspire AppHost at [`src/AspireApp`](/Users/robert/Projects/MyServiceBus/src/AspireApp) that starts:

- the C# test app
- the Java test app
- optional RabbitMQ infrastructure

## Why the Java app needs extra setup

The Java test app uses Aspire's Java hosting integration, which relies on the OpenTelemetry Java agent for automatic instrumentation.

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

The AppHost is configured to point to the agent directory relative to the Java app working directory:

`../../AspireApp/agents`

That resolves to:

`src/AspireApp/agents/opentelemetry-javaagent.jar`

The Java app also needs a trusted certificate PEM for Aspire's local OTLP endpoint:

`src/AspireApp/agents/aspire-localhost-cert.pem`

The AppHost passes that file to the Java agent through `OTEL_EXPORTER_OTLP_CERTIFICATE`.

See [`src/AspireApp/AppHost.cs`](/Users/robert/Projects/MyServiceBus/src/AspireApp/AppHost.cs).

## Java packaging requirement

Aspire launches the Java sample as an executable JAR:

`src/Java/testapp/build/libs/testapp-1.0-SNAPSHOT.jar`

That JAR must include its runtime dependencies. The Gradle configuration for [`src/Java/testapp/build.gradle`](/Users/robert/Projects/MyServiceBus/src/Java/testapp/build.gradle) builds a self-contained JAR so Aspire can launch it directly with `java -jar`.

Build it from the repository root:

```bash
./gradlew :testapp:jar
```

## Running locally with Aspire

1. Start RabbitMQ:

```bash
docker compose up -d rabbitmq
```

2. Build the Java executable JAR:

```bash
./gradlew :testapp:jar
```

3. Ensure the Aspire localhost certificate PEM exists:

```bash
cp /path/to/aspire/cert.pem src/AspireApp/agents/aspire-localhost-cert.pem
```

If the local Aspire certificate rotates, refresh `src/AspireApp/agents/aspire-localhost-cert.pem` with the current PEM before starting the AppHost again.

4. Start the Aspire AppHost:

```bash
dotnet run --project src/AspireApp/AspireApp.csproj
```

5. Open the Aspire dashboard URL printed by the AppHost.

## C# telemetry note

The C# app does not need an agent, but it does need to register the custom `MyServiceBus` activity source with OpenTelemetry so Aspire can export those spans.

See:

- [`src/MyServiceBus/OpenTelemetry.cs`](/Users/robert/Projects/MyServiceBus/src/MyServiceBus/OpenTelemetry.cs)
- [`src/TestApp/Program.cs`](/Users/robert/Projects/MyServiceBus/src/TestApp/Program.cs)
