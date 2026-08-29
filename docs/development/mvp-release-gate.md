# Foundation MVP Release Gate

## Release outcome

This gate records the foundation that later previews continue to protect. The C# and Java clients provide the ordinary RabbitMQ application path documented in the [MVP API Surface](mvp-api-surface.md) and make only the scoped compatibility claims in the [Compatibility Policy](../compatibility.md).

Preview releases after the foundation MVP also contain explicitly versioned inspection, monitoring, Azure Service Bus, mediator, scheduling, and PostgreSQL outbox surfaces. Their readiness is reported separately in the [API and Readiness Matrix](../api-readiness.md), [Enterprise Production Readiness](../enterprise-readiness.md), and feature-specific documentation. Including them in the same package line does not promote every surface to production-ready status.

## Completed product gates

- The C# and Java clients share canonical protocol and topology fixtures.
- RabbitMQ integration runs against disposable Testcontainers brokers with dynamically mapped ports.
- C#↔Java and MyServiceBus↔MassTransit scenarios cover the immediate conformance matrix in CI.
- Transport capabilities and startup validation distinguish native, emulated, and unsupported behavior.
- Runtime provisioning and inspection consume the normalized, versioned topology model.
- The profile-neutral receive-endpoint topology is the supported transport extension API; legacy overloads are deprecated adapters.
- The resolved .NET dependency graph has no known NuGet advisories, and CI rejects advisory-bearing restores.
- All intended preview NuGet and Maven artifacts build locally with required identity, licensing, repository, source, symbol, and Javadoc metadata; CI validates the exact artifact sets.
- Clean external-style C# and Java smoke projects restore, compile, and run against only the staged NuGet and Maven publications.
- The supported .NET, Java, RabbitMQ, MassTransit, and client-library baselines and the preview servicing window are explicit and checked against CI configuration.
- The public quick starts, walkthrough, two-service sample, Aspire workflow, Java helper script, and local documentation links have been audited from clean source state; preview inspection endpoints are labeled as unstable.

## Per-preview release gate

1. **Release candidate gate** — require the ordinary unit suites, RabbitMQ integration suite, complete interoperability matrix, dependency audit, and package verification on the same candidate commit.

## Release decision

Each preview can be tagged when the release-candidate gate is complete and the candidate commit passes CI. A failure in an experimental addon blocks the coordinated preview when that addon is one of its published artifacts, but its successful build does not upgrade the addon's documented readiness.

The current release work follows the [Enterprise Production Readiness](../enterprise-readiness.md) plan. Preview `0.1.0-preview.6` adds the Transactional Outbox evaluation MVP, durable one-time PostgreSQL scheduling and cancellation, and outbox dispatcher operations without claiming that the complete O01–O06 production matrix, Consumer Outbox middleware, retention cleanup, alerting, or durable monitoring history are finished.
