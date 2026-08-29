# Supported Versions

## MVP baseline

MyServiceBus `0.1.0-preview.6` is built and tested against the following baseline:

The baseline distinguishes language/API compatibility from the build and runtime used to verify a release. C# packages target `net10.0`, which selects the available .NET reference assemblies and BCL surface. Java publishes Java 17-compatible bytecode and APIs; the implementation should use modern Java 17 language features and JDK types where useful without requiring a newer bytecode level. Running on a newer JDK is an expected compatibility path, not the same as moving the published Java target.

| Component | Supported line | Reproducible CI baseline | Scope |
| --- | --- | --- | --- |
| .NET | .NET 10 | .NET SDK `10.0.100`, with latest-patch roll-forward | C# packages target `net10.0`. Use a supported .NET 10 servicing release. |
| Java | Java 17 or newer | Temurin Java 17 | Published bytecode and APIs target Java 17. Newer Java releases are expected to work but are not release-gating environments. |
| Gradle | Gradle 9.0 | Gradle `9.0.0` | Build and Maven publication tooling; not an application runtime dependency. |
| PostgreSQL | PostgreSQL 17 | Docker image `postgres:17.6-alpine` | Transactional outbox/inbox persistence baseline. The provider is not production-promoted until the remaining integration and O01–O06 gates pass. |
| .NET PostgreSQL client | Npgsql 10.0 | `10.0.3` | Implementation and public transaction boundary of the C# PostgreSQL provider. |
| Java PostgreSQL client | pgJDBC 42.7 | `42.7.13` | Implementation and public transaction boundary of the Java PostgreSQL provider. |
| RabbitMQ server | RabbitMQ 4.1 | Docker image `rabbitmq:4.1.8-alpine` | The declared RabbitMQ transport-profile baseline. Other broker lines are not yet claimed as supported. |
| MassTransit | MassTransit 8.5 | `MassTransit.RabbitMQ` `8.5.1` | The exact interoperability peer. Compatibility with other MassTransit versions must not be inferred from this baseline. |
| NServiceBus | NServiceBus 10.2 | `NServiceBus` `10.2.8` with `NServiceBus.RabbitMQ` `11.2.1` | Exact peer for the separate RabbitMQ directed-send profile. Other NServiceBus features and versions are not implied. |
| Azure Service Bus | Standard tier | Live Azure Standard namespace | The declared Azure transport-profile baseline. Premium is expected to support this slice but is not the release-gating environment. Basic is unsupported because it lacks topics and subscriptions. |
| MassTransit Azure Service Bus | MassTransit 8.5 | `MassTransit.Azure.ServiceBus.Core` `8.5.1` | The exact Azure interoperability peer. Compatibility with other MassTransit versions must not be inferred from this baseline. |
| .NET RabbitMQ client | RabbitMQ.Client 7.2 | `7.2.1` | Implementation dependency of the C# RabbitMQ transport. |
| Java RabbitMQ client | AMQP client 5.20 | `com.rabbitmq:amqp-client` `5.20.0` | Implementation dependency of the Java RabbitMQ transport. |
| Azure Service Bus emulator | Emulator 2.0 | `mcr.microsoft.com/azure-messaging/servicebus-emulator:2.0.1` | Local data-plane baseline for the Azure Service Bus adapters; not a cloud-fidelity claim. |
| .NET Azure Service Bus client | Azure SDK 7.20 | `Azure.Messaging.ServiceBus` `7.20.2` | Implementation dependency of the C# adapter. |
| Java Azure Service Bus client | Azure SDK 7.17 | `com.azure:azure-messaging-servicebus` `7.17.20` | Implementation dependency of the Java adapter. |

"Supported" means that the release candidate passes the ordinary unit suites,
package-consumer smoke tests, broker integration tests, and the declared
cross-language and MassTransit interoperability matrix. Azure support remains
limited to the scenarios marked implemented in the transport profile. A
baseline does not imply that every combination in a wider major-version range
has been tested.

## Preview support window

Before `1.0`, only the newest published MyServiceBus preview is actively supported. A new preview replaces the previous preview's support window. Fixes are delivered in a newer preview; the project does not promise servicing releases for older previews.

The runtime lines above remain the baseline for the lifetime of `0.1.0-preview.6`. Security and servicing patches within .NET 10 and Java 17 are supported and recommended. Changing the target framework, Java bytecode level, RabbitMQ minor line, or MassTransit interoperability peer requires an explicit update to this document and a passing release gate.

## Compatibility boundaries

- RabbitMQ `4.1.8`, live Azure Service Bus Standard, and the respective MassTransit `8.5.1` transports are evidence-backed interoperability targets, not broad promises for every broker or MassTransit 8.x release.
- NServiceBus `10.2.8` with its RabbitMQ transport `11.2.1` is evidence for the directed-send scenarios in the separate [NServiceBus profile](nservicebus-interoperability.md), not general NServiceBus compatibility.
- Java releases newer than 17 and .NET 10 servicing releases are runtime compatibility expectations. A defect reproduced only outside the CI baseline may require a new conformance job before it becomes release-blocking.
- End-of-life runtime or broker releases are not supported, even if they happen to run the packages.
- Support for another broker, MassTransit version, target framework, or Java baseline begins only when it is named here and covered by the appropriate conformance suite.

The broader meaning and levels of compatibility are defined in the [Compatibility Policy](compatibility.md).
