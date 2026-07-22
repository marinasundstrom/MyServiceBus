# MVP Release Gate

## Release outcome

The MVP is a stable C# and Java messaging foundation for RabbitMQ. It supports the ordinary application path documented in the [MVP API Surface](mvp-api-surface.md) and makes only the scoped compatibility claims in the [Compatibility Policy](../compatibility.md).

The MVP is not an inspection, dashboard, saga, outbox, or multi-transport release. Those features build on the MVP after its protocol, topology, transport, and lifecycle contracts are released and can evolve deliberately.

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

## Remaining release gates

1. **Release candidate gate** — require the ordinary unit suites, RabbitMQ integration suite, complete interoperability matrix, dependency audit, and package verification on the same candidate commit.

## Release decision

The MVP can be tagged when all remaining gates are complete and the candidate commit passes CI. Work from roadmap Phase 3 onward must not delay that tag unless it reveals a defect in an MVP contract.

After the MVP tag, the next product increment is stabilization of the optional inspection DTOs against the released topology model. Monitoring history and a read-only dashboard follow only after those programmatic APIs are stable.
