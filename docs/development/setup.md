# Setup

This is the development setup.

## Projects

.NET client, written in C#
* `MyServiceBus` - the base library for the service bus
* `MyServiceBus.Abstractions` - common abstractions, like `IConsumer`
* `MyServiceBus.RabbitMQ` - implementing the RabbitMQ transport
* `TestApp` - our test project for the .NET client

Java client
* `myservicebus` - the actual service bus. At the moment, containing the RabbitMQ transport.
* `testapp` - our test project for the Java client

Reference - for compatibility with MassTransit

* `TestApp_MassTransit` - a project using MassTransit, for reference

## Packaging

Build distributable NuGet packages for the .NET libraries from the repository root:

```bash
dotnet pack src/MyServiceBus.Abstractions/MyServiceBus.Abstractions.csproj \
  --configuration Release \
  -p:PackageOutputPath=artifacts/packages/dotnet

dotnet pack src/MyServiceBus/MyServiceBus.csproj \
  --configuration Release \
  -p:PackageOutputPath=artifacts/packages/dotnet

dotnet pack src/MyServiceBus.RabbitMq/MyServiceBus.RabbitMq.csproj \
  --configuration Release \
  -p:PackageOutputPath=artifacts/packages/dotnet

dotnet pack src/MyServiceBus.Testing/MyServiceBus.Testing.csproj \
  --configuration Release \
  -p:PackageOutputPath=artifacts/packages/dotnet
```

Build distributable Maven packages for the Java libraries from the repository root:

```bash
gradle publishAllPublicationsToPackagesRepository
```

The Gradle publications are written to `build/packages/maven`.

GitHub Actions runs both packaging flows in `.github/workflows/build-packages.yml` and uploads the generated package directories as workflow artifacts.

## Logging Emojis

The sample applications use emojis to highlight message flow and results:

- 🚀 start
- 📤 outgoing message
- 📨 incoming message
- ✅ success
- ❌ failure
- ⚠️ warning or handled error
- ℹ️ informational note

## RabbitMQ

```
docker compose up
```

Management: `http://127.0.0.1:15672/`

Username: `guest`

Password: `guest`
