# Testing MyServiceBus

MyServiceBus ships with an **in-memory test harness** for both the C# and Java clients. The harnesses share the same design so tests exercise the bus without requiring a running transport.

## Protocol Conformance Fixtures

Versioned fixtures under `test/fixtures/protocol/v1` define canonical message, request, and fault envelopes. Both the C# and Java suites load the same files and validate that their public envelope and fault models interpret portable metadata consistently.

Add a new fixture when wire behavior changes intentionally. Do not edit an existing protocol version in a way that invalidates released clients; introduce a new fixture version instead.

## RabbitMQ Integration Tests

RabbitMQ transport tests use Testcontainers to start a disposable broker. Docker or another Testcontainers-compatible container runtime must be available.

The tests:

- pin the RabbitMQ image to the exact version declared in [Supported Versions](supported-versions.md)
- use dynamically mapped ports
- create unique exchanges and queues
- exercise the real send and receive transports
- stop the receive transport and remove the container after completion

Run the complete suites from the repository root:

```bash
dotnet test
gradle test
```

The first container-backed run may be slower while the RabbitMQ image is downloaded.

## PostgreSQL Outbox and Inbox Integration Tests

The PostgreSQL provider suites use Testcontainers with the exact server version declared in [Supported Versions](supported-versions.md). They validate idempotent schema creation, caller-transaction commit and rollback, complete persisted-envelope rehydration, disjoint leases across competing dispatcher stores, and atomic inbox completion with an outgoing outbox record.

Run the focused suites from the repository root:

```bash
dotnet test test/MyServiceBus.PostgreSql.Tests/MyServiceBus.PostgreSql.Tests.csproj
gradle :myservicebus-postgresql:test
```

These are provider integration tests, separate from the portable dispatcher unit suites. They are not yet the O01–O06 crash-injection, cleanup, schema-rollout, broker-dispatch, and source-settlement promotion matrix.

## Azure Service Bus Emulator Fixture

A pinned Docker Compose fixture under `test/AzureServiceBusEmulator` prepares
the local topology for the C# and Java Azure Service Bus preview
transports. Both suites exercise direct queue delivery, topic publication with
subscription forwarding, their corresponding public factory configuration
paths, retry recovery and exhaustion, `_error` and `_skipped` settlement, and
endpoint fault publication against this shared topology. Each client also runs
two receivers against one queue and verifies that every message is delivered
once while both competing consumers participate. Request tests map
generated temporary endpoint names to the fixture's sequential `msb-response`
queue and cover responses and faults. The .NET suite also launches the Java
interoperability peer to prove directed send and publish in both language
directions, including correlated requests and responses. Those cross-language
delivery checks also verify envelope identifiers, addresses, application
headers, and native message, correlation, reply-to, subject, and destination
properties.

Validate the checked-in JSON and Compose configuration with:

```bash
sh eng/verify-servicebus-emulator.sh
```

After starting the fixture, run the data-plane suites sequentially:

```bash
RUN_AZURE_SERVICEBUS_EMULATOR_TESTS=1 \
  dotnet test test/MyServiceBus.AzureServiceBus.Tests/MyServiceBus.AzureServiceBus.Tests.csproj
RUN_AZURE_SERVICEBUS_EMULATOR_TESTS=1 \
  gradle :myservicebus-azure-service-bus:test --rerun-tasks
```

Ordinary test runs skip the emulator scenarios. The cross-language cases also
require Java 17 and system Gradle. These tests prove the local data plane only;
cloud topology creation, identity, and MassTransit Azure Service Bus
interoperability remain separate gates.

The optional cloud gate includes C# and Java acceptance tests that start with
unique entity names, use MyServiceBus `Create` mode to provision queues,
topics, forwarding subscriptions, and compatibility destinations, then publish,
consume, inspect the resulting topology, and clean it up. Run the configured
development namespace without copying its SAS key into the shell:

```bash
eng/run-azure-servicebus-cloud-tests.sh
```

The runner defaults to resource group `rg-myservicebus-tests`, Standard
namespace `sb-myservicebus-tests-se`, and authorization rule
`MyServiceBusTests`. Override those names with
`AZURE_SERVICEBUS_RESOURCE_GROUP`, `AZURE_SERVICEBUS_NAMESPACE`, and
`AZURE_SERVICEBUS_AUTHORIZATION_RULE`. The signed-in Azure CLI principal must
be allowed to list authorization-rule keys. The connection string exists only
in the runner process environment and is not written to the repository.

The namespace lifecycle can be managed explicitly:

```bash
eng/manage-azure-servicebus-cloud-tests.sh provision
eng/manage-azure-servicebus-cloud-tests.sh status
eng/manage-azure-servicebus-cloud-tests.sh teardown
```

`teardown` waits until the exact configured namespace has been deleted. It
does not delete the resource group or any other resource. For an isolated
one-shot run, provision a globally unique namespace and guarantee teardown on
success, test failure, or interruption with:

```bash
eng/run-azure-servicebus-cloud-tests.sh --ephemeral
```

The ephemeral runner deliberately ignores `AZURE_SERVICEBUS_NAMESPACE` and
generates a unique namespace. Set `AZURE_SERVICEBUS_EPHEMERAL_NAMESPACE` only
when a predictable one-shot name is required. The free resource group remains
afterward so its deletion can never remove unrelated resources.

The repeatable live-test procedure is:

1. Sign in with `az login` and select the intended subscription with
   `az account set` when necessary.
2. Run `eng/run-azure-servicebus-cloud-tests.sh --ephemeral` for an isolated
   acceptance run, or provision once and use the runner without an argument for
   the persistent development namespace.
3. Let the .NET-hosted gate finish before the standalone Java gate. The first
   also launches the Java interoperability peer for the pinned MassTransit
   producer and consumer cases. Endpoint queues are unique; the default-naming
   cases intentionally share the formatter-derived message topic and remove it
   after each sequential case. Test cleanup removes queues, topics, and
   subscriptions.
4. Confirm that both gates report success. They verify Create-mode topology,
   delivery-lock renewal while a handler runs beyond the initial lock duration,
   publish/forward/consume behavior, correlated request/response, and the
   five-minute native auto-delete setting on temporary response queues. The
   MassTransit cases additionally verify default message-topic naming and
   bidirectional publication plus directed queue sends in every MassTransit and
   MyServiceBus client direction. C# and Java MyServiceBus request clients also
   verify correlated responses from MassTransit through unique native temporary
   queues, while MassTransit request clients verify correlated responses from
   C# and Java MyServiceBus services. The same four directions verify correlated
   fault responses and their MassTransit-compatible exception details. When a
   C# or Java MyServiceBus handler fails, the gate additionally verifies the
   original MassTransit request and exception metadata in `_error` and confirms
   that the primary queue drains after the compatibility copy succeeds.
5. For persistent use, run the explicit `teardown` command when the namespace is
   no longer needed. Ephemeral runs do this automatically in an exit trap.

Create an equivalent isolated namespace and test rule with:

```bash
az group create \
  --name rg-myservicebus-tests \
  --location swedencentral \
  --tags project=MyServiceBus purpose=integration-testing
az servicebus namespace create \
  --resource-group rg-myservicebus-tests \
  --name '<globally-unique-namespace>' \
  --location swedencentral \
  --sku Standard \
  --tags project=MyServiceBus purpose=integration-testing
az servicebus namespace authorization-rule create \
  --resource-group rg-myservicebus-tests \
  --namespace-name '<globally-unique-namespace>' \
  --name MyServiceBusTests \
  --rights Manage Send Listen
```

Standard is required because Basic does not support topics and subscriptions.
Do not pre-provision messaging entities: an empty namespace is part of the
acceptance condition.

For a manually supplied namespace connection string, run the individual gates
with:

```bash
RUN_AZURE_SERVICEBUS_CLOUD_TESTS=1 \
AZURE_SERVICEBUS_CLOUD_CONNECTION_STRING='<connection-string>' \
  dotnet test test/MyServiceBus.AzureServiceBus.Tests/MyServiceBus.AzureServiceBus.Tests.csproj \
  --filter FullyQualifiedName~AzureServiceBusCloudAcceptanceTests
RUN_AZURE_SERVICEBUS_CLOUD_TESTS=1 \
AZURE_SERVICEBUS_CLOUD_CONNECTION_STRING='<connection-string>' \
  gradle :myservicebus-azure-service-bus:test --rerun-tasks \
  --tests com.myservicebus.azure.servicebus.AzureServiceBusCloudTest
```

The self-provisioning MassTransit cloud cases are included by the runner. To run
only those cases, use the `MassTransitAzureServiceBusInteropTests` .NET filter.

See the fixture README and the [Azure Service Bus transport profile](azure-service-bus-transport.md)
for its topology, connection string, sequential-test constraint, and cloud
smoke-test boundary.

### Cross-language RabbitMQ matrix

The interoperability matrix uses one Testcontainers broker per scenario. It covers C#↔Java and bidirectional envelope delivery, correlated request/response, correlated fault responses, retry exhaustion, and preservation in MassTransit-readable `_error` and `_skipped` queues for both reference clients and the pinned MassTransit version. Java scenarios launch the Java interoperability peer from the .NET test process. They require .NET, Java 17, Gradle, and Docker.

```bash
RUN_CROSS_LANGUAGE_TESTS=1 \
  dotnet test test/MyServiceBus.RabbitMq.Tests/MyServiceBus.RabbitMq.Tests.csproj \
  --filter "CrossLanguageRabbitMqTests|MassTransitInteropTests"
```

Ordinary test runs report these scenarios as skipped. The dedicated cross-language CI workflow enables them explicitly.

## Goals
- Mirror MassTransit's `InMemoryTestHarness` so existing users feel at home.
- Keep the C# and Java harness implementations aligned, ensuring features and default behavior remain consistent across languages.
- Keep test observations deterministic and separate from the production mediator API.
- Verify the shared scenarios in the [Mediator and In-Memory Stability Gate](development/in-memory-stability-gate.md) before adding another broker transport.

## Usage
The pattern is identical in both languages: create the harness, register handlers, start it, send messages, assert consumption, and then stop the harness.

The harness starts in the stopped state. `Start`/`start` and `Stop`/`stop` are idempotent, and a stopped harness may be started again. Send, publish, and request operations before start or after stop fail with the platform's invalid-state exception. Handler and consumer registration remains valid while stopped so a test can be fully configured before it starts delivery.

The standalone mediator has a different responsibility: it is immediately usable after construction and does not model a hosted transport lifecycle. A hosted broker-backed bus still follows its host or explicit bus lifecycle.

Consumers registered through the dependency-injection configuration receive a new service scope for every delivery. That scope remains alive until the consumer's asynchronous operation completes and is then disposed, including asynchronous disposal in C#. Direct handler delegates are application-owned callbacks and do not create a dependency-injection scope.

### C#
```csharp
var harness = new InMemoryTestHarness();
harness.RegisterHandler<SomeMessage>(ctx => Task.CompletedTask);

await harness.Start();
var consumed = harness.WaitForConsumed<SomeMessage>(TimeSpan.FromSeconds(1));
await harness.Publish(new SomeMessage());
Assert.True(await consumed);
await harness.Stop();
```

### Java
```java
InMemoryTestHarness harness = new InMemoryTestHarness();
harness.registerHandler(SomeMessage.class, ctx -> CompletableFuture.completedFuture(null));

harness.start().join();
CompletableFuture<Boolean> consumed = harness.waitForConsumed(
        SomeMessage.class, Duration.ofSeconds(1));
harness.send(new SomeMessage()).join();
assertTrue(consumed.join());
harness.stop().join();
```

These helpers enable fast, isolated tests and provide the same API surface in both languages, supporting the project's alignment goals.

`WaitForConsumed<T>` and `waitForConsumed` first inspect existing observations and then wait for a future successful consumer completion until the explicit timeout. They return `false` when the timeout elapses. C# caller cancellation remains distinct and throws `OperationCanceledException`; Java callers may cancel the returned `CompletableFuture` using the normal Java future API.

The current shared observation category is **consumed**, recorded once for each consumer pipeline that completes successfully. A single message therefore creates multiple consumed observations when multiple compatible consumers succeed. Failed attempts are not consumed observations. Sent, published, faulted, and scheduled observation collections remain future testing features and are not implied by the current harness API.

Scheduling tests can replace `IJobScheduler` or `JobScheduler` with a manually controlled implementation. This lets a test verify scheduled publish, directed send, and cancellation by explicitly releasing or removing a pending callback instead of sleeping against wall-clock time. The callback completes only after the resulting local delivery completes. The local scheduler does not promise ordering between messages with the same due time.

## Publishing from a service class
Classes can inject `IPublishEndpoint` (C#) or `PublishEndpoint` (Java) and be verified with the in-memory harness.

### C#
```csharp
record ValueSubmitted(Guid Value);

class PublishingService
{
    readonly IPublishEndpoint publishEndpoint;

      public PublishingService(IPublishEndpoint publishEndpoint) => this.publishEndpoint = publishEndpoint;

      public Task Submit(Guid value) => publishEndpoint.Publish(new ValueSubmitted(value));
}

var services = new ServiceCollection();
services.AddServiceBusTestHarness();
services.AddScoped<PublishingService>();

var provider = services.BuildServiceProvider();
var harness = provider.GetRequiredService<InMemoryTestHarness>();
harness.RegisterHandler<ValueSubmitted>(_ => Task.CompletedTask);

await harness.Start();
await provider.GetRequiredService<PublishingService>().Submit(Guid.NewGuid());

Assert.True(harness.WasConsumed<ValueSubmitted>());
await harness.Stop();
```

### Java
```java
record ValueSubmitted(UUID value) { }

class PublishingService {
    private final PublishEndpoint publishEndpoint;

    PublishingService(PublishEndpoint publishEndpoint) {
        this.publishEndpoint = publishEndpoint;
    }

    CompletableFuture<Void> submit(UUID value) {
        return publishEndpoint.publish(new ValueSubmitted(value));
    }
}

ServiceCollection services = ServiceCollection.create();
TestingServiceExtensions.addServiceBusTestHarness(services, cfg -> {});
services.addScoped(PublishingService.class);

ServiceProvider provider = services.buildServiceProvider();
InMemoryTestHarness harness = provider.getService(InMemoryTestHarness.class);
harness.handler(ValueSubmitted.class, ctx -> CompletableFuture.completedFuture(null));

harness.start();
try (ServiceScope scope = provider.createScope()) {
    ServiceProvider sp = scope.getServiceProvider();
    sp.getService(PublishingService.class).submit(UUID.randomUUID()).join();
}

assertTrue(harness.consumed().any(ValueSubmitted.class));
harness.stop();
```
