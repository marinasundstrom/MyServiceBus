# Explicit message contract migration

This preview changes Java convention-based URNs and entity names from the hardcoded
`TestApp` namespace to the actual Java package. A class `com.acme.orders.OrderSubmitted`
now defaults to `urn:message:com.acme.orders:OrderSubmitted` and the entity
`com.acme.orders:OrderSubmitted`. This is a wire and topology change, not an alias.

Before a rolling deployment, assign the existing shared URN and entity name explicitly
on both publishers and consumers. For an existing Java `TestApp` deployment:

```java
@MessageUrnName(value = "urn:message:TestApp:OrderSubmitted", useDefaultPrefix = false)
@EntityName("TestApp:OrderSubmitted")
public record OrderSubmitted(String orderId) { }
```

Keep those identities until old producers, stored outbox records, and queued messages
have drained. Renaming the contract requires a coordinated migration or application
bridge; the client does not silently accept old URNs or provision old-name aliases.
Unknown envelopes remain inspectable in `_skipped`. Do not blindly replay error or
skipped queues before correcting the contract or payload.

C# default non-generic nested type URNs now include declaring type names separated by `+`, avoiding
collisions between otherwise identically named nested types. Assign an explicit URN
to retain an existing nested contract. Java nested classes retain their simple-name
convention. Use explicit identities for portable contracts, especially nested and
generic contracts; matching native type names is not a cross-language identity policy. Runtime
implementation interfaces such as `System.IEquatable<T>` and Java platform base types
are excluded from inherited advertised contracts.

URN and broker entity names remain independent. RabbitMQ formatters are held by each
bus configurator, and per-message names apply to publishing and consumer bindings.
The legacy static formatter affects direct formatter calls and the initial default
of subsequently created configurators; configuring a bus no longer mutates it.
One logical bus per application remains the supported hosting model.

Bus-level URN overrides win over attributes/annotations and are frozen after
configuration. They apply to normal envelope publication, receive matching, requests,
outbox message metadata, and topology snapshots. Choreography and saga declarations
that already contain explicit string URNs remain application-owned; use the same
identity in those declarations. Startup rejects declarations using a known contract
identity that a bus-level override has superseded, and identifies the required replacement. Snapshot version 3 identifies the changed contract
identity construction; version 1 and 2 fixtures remain historical evidence.

RabbitMQ inherited publication is covered by broker tests, including C# to Java and
Java to C# with different local type names, and bidirectional publication with
MassTransit 8.5.1. Azure Service Bus create-topology mode now provisions direct forwarding subscriptions
from the selected topic to each distinct base/interface topic. Pre-provisioned mode
requires those subscriptions to be deployed separately; the forwarding subscription
name is `msb-` followed by the first 32 lowercase hexadecimal characters of the
SHA-256 hash of the destination topic's UTF-8 name. Provisioning is covered by SDK
contract tests and publisher-only interface-delivery tests against live Azure.
Forwarding remains at-least-once: overlapping forwarding routes or subscriptions
can deliver duplicates, and Azure forwarding hop limits still apply.

Amazon SNS/SQS keeps selected-contract publication: SNS does not provide topic-to-topic
bindings, and the [pinned MassTransit 8.5.1 SNS publish topology](https://github.com/MassTransit/MassTransit/blob/v8.5.1/src/Transports/MassTransit.AmazonSqsTransport/AmazonSqsTransport/Topology/AmazonSqsMessagePublishTopology.cs) likewise creates the
selected topic without inherited bindings. Publish with the interface/base contract
explicitly when subscribing to that contract. Both cloud adapters now match all URNs
on received envelopes. This separates receive contract matching from broker routing;
it does not claim universal transport equivalence.

Queue-per-handler and per-consumer retry policies remain valid integration patterns. Runtime-type dispatch and startup contract validation are
reasonable integration-layer choices. Expanding every interface subscription into
concrete subscriptions and invoking post-build actions manually were workarounds;
ordinary RabbitMQ interface subscriptions and standalone bus startup now cover those
cases. Startup serialization validation is optional application policy, and custom
serializers still need their own contract coverage.
