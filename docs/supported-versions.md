# Supported Versions

## MVP baseline

MyServiceBus `0.1.0-preview.1` is built and tested against the following baseline:

| Component | Supported line | Reproducible CI baseline | Scope |
| --- | --- | --- | --- |
| .NET | .NET 10 | .NET SDK `10.0.100`, with latest-patch roll-forward | C# packages target `net10.0`. Use a supported .NET 10 servicing release. |
| Java | Java 17 or newer | Temurin Java 17 | Published bytecode and APIs target Java 17. Newer Java releases are expected to work but are not release-gating environments. |
| Gradle | Gradle 9.0 | Gradle `9.0.0` | Build and Maven publication tooling; not an application runtime dependency. |
| RabbitMQ server | RabbitMQ 4.1 | Docker image `rabbitmq:4.1.8-alpine` | The declared RabbitMQ transport-profile baseline. Other broker lines are not yet claimed as supported. |
| MassTransit | MassTransit 8.5 | `MassTransit.RabbitMQ` `8.5.1` | The exact interoperability peer. Compatibility with other MassTransit versions must not be inferred from this baseline. |
| .NET RabbitMQ client | RabbitMQ.Client 7.2 | `7.2.1` | Implementation dependency of the C# RabbitMQ transport. |
| Java RabbitMQ client | AMQP client 5.20 | `com.rabbitmq:amqp-client` `5.20.0` | Implementation dependency of the Java RabbitMQ transport. |

"Supported" means that the release candidate passes the ordinary unit suites, package-consumer smoke tests, RabbitMQ integration tests, and the declared cross-language and MassTransit interoperability matrix. It does not mean that every combination in a wider major-version range has been tested.

## Preview support window

Before `1.0`, only the newest published MyServiceBus preview is actively supported. A new preview replaces the previous preview's support window. Fixes are delivered in a newer preview; the project does not promise servicing releases for older previews.

The runtime lines above remain the baseline for the lifetime of `0.1.0-preview.1`. Security and servicing patches within .NET 10 and Java 17 are supported and recommended. Changing the target framework, Java bytecode level, RabbitMQ minor line, or MassTransit interoperability peer requires an explicit update to this document and a passing release gate.

## Compatibility boundaries

- RabbitMQ `4.1.8` and MassTransit `8.5.1` are evidence-backed interoperability targets, not broad promises for all RabbitMQ 4.x or MassTransit 8.x releases.
- Java releases newer than 17 and .NET 10 servicing releases are runtime compatibility expectations. A defect reproduced only outside the CI baseline may require a new conformance job before it becomes release-blocking.
- End-of-life runtime or broker releases are not supported, even if they happen to run the packages.
- Support for another broker, MassTransit version, target framework, or Java baseline begins only when it is named here and covered by the appropriate conformance suite.

The broader meaning and levels of compatibility are defined in the [Compatibility Policy](compatibility.md).
