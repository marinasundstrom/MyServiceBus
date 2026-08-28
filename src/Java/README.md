# MyServiceBus Java

This folder contains the Java modules for MyServiceBus. The Gradle multi-project build resides at the repository root.

## Prerequisites
- JDK 17 (Temurin/OpenJDK recommended)
- Gradle
- Docker (optional) to run RabbitMQ locally

## Build

- From the repository root, build all modules and run tests with the system Gradle installation:
  ```bash
  gradle test
  ```

## New project: decorator style

For a new Java project, prefer the MyServiceBus service collection and decorator structure:

```java
ServiceCollection services = ServiceCollection.create();

services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            cfg.addConsumer(SubmitOrderConsumer.class);
            cfg.using(RabbitMqFactoryConfigurator.class,
                    (context, rabbit) -> rabbit.configureEndpoints(context));
        });

ServiceProvider provider = services.buildServiceProvider();
MessageBus bus = provider.getRequiredService(MessageBus.class);
bus.start();
```

The decorator structure is the Java equivalent of the extension-method composition style used by the C# client.

## Existing application: bus factory

Use the bus factory when adding MyServiceBus to a Java application that already owns its construction or dependency-injection model. Application code does not need to build or resolve through a MyServiceBus service provider:

```java
MessageBus bus = MessageBus.factory.create(RabbitMqFactoryConfigurator.class, cfg -> {
    cfg.host("localhost");
    cfg.receiveEndpoint("submit-order", endpoint ->
            endpoint.handler(SubmitOrder.class, context ->
                    new SubmitOrderConsumer().consume(context)));
});

bus.start();
```

The handler may resolve a consumer from an existing Spring, CDI, Dagger, Guice, or application-owned container. That container is not installed into the default Guice provider:

```java
endpoint.handler(SubmitOrder.class, context ->
        applicationContext.getBean(SubmitOrderConsumer.class).consume(context));
```

## Generated consumer catalogs and AOT

Java applications can register consumers explicitly or use the optional, framework-neutral JSR 269 processor:

```groovy
dependencies {
    implementation 'io.github.marinasundstrom.myservicebus:myservicebus:0.1.0-preview.5'
    annotationProcessor 'io.github.marinasundstrom.myservicebus:myservicebus-processor:0.1.0-preview.5'
}
```

Annotate consumer methods with `@MessageConsumer`, then register the generated catalog with `GeneratedConsumerCatalog.INSTANCE::register`. This avoids runtime method discovery and reflective invocation. The GraalVM Native Image path is an MVP proof of concept and remains work in progress; run its end-to-end smoke test from the repository root with:

```bash
./eng/verify-java-aot.sh
```

The MyServiceBus `ServiceCollection` and decorator structure form a container-neutral programming model. `ServiceCollection.create()` materializes it with the included Guice-backed implementation, while a framework adapter can materialize the same registrations with another container. `ServiceCollection.createAot()` selects a factory-only implementation with no reflective constructor activation. An existing application may instead use an explicit bus factory that closes over Spring, CDI, Dagger, Guice, or an application-owned resolver without adopting the MyServiceBus provider model. See [Java dependency-injection boundary](../../docs/development/java-dependency-injection.md).

Zero-argument service registrations accept the JDK-standard `Supplier<? extends T>`. Providers from `javax.inject`, `jakarta.inject`, Spring, Dagger, or another framework adapt through method references such as `provider::get` or `provider::getObject`; MyServiceBus does not select one DI namespace for its core API.

## Run locally
### 1) Start RabbitMQ
From the repository root, start RabbitMQ using Docker Compose:
```bash
docker compose up -d rabbitmq
```
RabbitMQ defaults: host `localhost`, port `5672`, mgmt UI `http://localhost:15672` (guest/guest).

### 2) Run the Test App

- From the repository root:
  ```bash
  RABBITMQ_HOST=localhost HTTP_PORT=5301 gradle :testapp:run
  ```

Helper script (equivalent):
```bash
cd src/Java/testapp
RABBITMQ_HOST=localhost HTTP_PORT=5301 ./run.sh
```

The app starts an HTTP server (default port 5301) with routes:
- `GET /publish` – publishes SubmitOrder
- `GET /send` – sends SubmitOrder to a queue
- `GET /request` – request/response demo
- `GET /request_multi` – request/response with fault handling

Run multiple instances by changing `HTTP_PORT` (e.g., 5301 and 5302).

### Environment variables
- `RABBITMQ_HOST`: RabbitMQ host (default `localhost`)
- `HTTP_PORT`: HTTP port for testapp (default `5301`)

## Aspire and OpenTelemetry

To run the Java test app under Aspire, two files must be present:

1. The OpenTelemetry Java agent JAR must exist at `src/AspireApp/agents/opentelemetry-javaagent.jar`.
2. A trusted Aspire OTLP certificate PEM must exist at `src/AspireApp/agents/aspire-localhost-cert.pem`.

The AppHost supervises the Java application's Gradle `run` task, so no separate JAR build is required.

Set up the Java agent:

```bash
mkdir -p src/AspireApp/agents
curl -fL \
  https://github.com/open-telemetry/opentelemetry-java-instrumentation/releases/latest/download/opentelemetry-javaagent.jar \
  -o src/AspireApp/agents/opentelemetry-javaagent.jar
```

The AppHost also configures the Java agent to trust a local Aspire OTLP certificate PEM at:

`src/AspireApp/agents/aspire-localhost-cert.pem`

Then start Aspire:

```bash
dotnet run --project src/AspireApp/AspireApp.csproj
```

Additional details are documented in [`docs/development/aspire-java-telemetry.md`](../../docs/development/aspire-java-telemetry.md).

See also: [`docs/two-service-sample.md`](../../docs/two-service-sample.md) for running .NET and Java together.

## Notes
- Lombok is configured as an annotation processor via the root `build.gradle` and does not need to be added per-module.
- Modules and external dependency versions are centralized in the root `build.gradle`.
