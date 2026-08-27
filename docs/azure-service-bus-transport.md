# Azure Service Bus Transport Profile

## Status

This document defines the proposed Azure Service Bus transport profile. The
transport is not implemented or supported yet. The profile fixes the first
implementation boundary so the C# and Java adapters can be developed against
the same addressing, topology, settlement, and failure semantics.

Azure Service Bus is a durable bus transport. It implements the existing
transport contract rather than introducing stream concepts into the portable
API.

## Initial Scope

The first implementation slice covers:

- directed send to a queue
- publish to a topic
- receive from a queue using peek-lock settlement
- topic subscriptions that forward published messages to endpoint queues
- competing consumers on an endpoint queue
- MassTransit-envelope serialization and native property mapping
- in-process retry through the portable consume pipeline
- `_error` and `_skipped` queues and endpoint-specific `Fault<T>` publication
- request/response using a transport-created response queue
- C# and Java data-plane interoperability

The first slice does not expose sessions, duplicate detection, native scheduled
enqueue, deferral, delayed redelivery, transactions, partitioning, or custom
subscription rules as public configuration. Those features require their own
cross-language behavior and conformance scenarios before they are advertised.

## Capability Descriptor

The initial adapters should publish this version 1 descriptor:

| Capability | Initial support | Profile constraint |
| --- | --- | --- |
| `directedSend` | Native | Sends directly to a queue. |
| `publishSubscribe` | Native | Publishes to a topic; subscriptions forward to endpoint queues. |
| `durability` | Native | Durable queues, topics, subscriptions, and messages. |
| `competingConsumers` | Native | Receivers compete on the same endpoint queue. |
| `acknowledgement` | Native | Peek-lock delivery is completed only after the consume pipeline succeeds. |
| `requestResponse` | Emulated | Composed from messages, correlation, and a response queue. |
| `scheduling` | Emulated | Uses the portable job scheduler until native scheduled enqueue is specified. |
| `retry` | Emulated | Re-invokes the consume pipeline while the delivery lock is held. |
| `redelivery` | Unsupported | Abandon, defer, and scheduled redelivery are not initially exposed. |
| `errorDestinations` | Emulated | Failed and skipped messages are copied to compatibility queues. |
| `ordering` | Native | Preserves broker enqueue order within a non-session entity; concurrent processing may complete out of order. |
| `replay` | Unsupported | Service Bus does not expose retained-history replay through this profile. |
| `temporaryEndpoints` | Native | Uses short-lived queues in Azure; the emulator fixture uses a pre-provisioned response queue. |
| `topologyProvisioning` | Native | Cloud namespaces use the administration SDK; emulator tests may use declarative topology. |

The descriptor reports behavior implemented by the adapter, not every feature
available in the broker. For example, Azure Service Bus supports native
scheduling, but the initial adapter continues to report portable scheduling as
emulated until that path is implemented and tested.

## Addressing

The profile accepts logical addresses for application convenience and produces
absolute addresses for serialized envelope fields.

| Destination | Logical form | Absolute form |
| --- | --- | --- |
| Queue | `queue:<name>` | `sb://<namespace>/<name>` |
| Topic | `topic:<name>` | `sb://<namespace>/<name>?type=topic` |

An absolute `sb` address without a `type` query value identifies a queue. The
only initially supported `type` value is `topic`. Entity paths are URI-escaped
when addresses are produced and decoded exactly once when resolved.

For Azure, `<namespace>` is the fully qualified namespace such as
`example.servicebus.windows.net`. For the local emulator it is the configured
data-plane host, normally `localhost`. The emulator's fixed internal namespace
name is configuration state and is not inserted into entity paths.

The factory owns all externally visible address production:

- `GetPublishAddress(entityName)` returns the topic address.
- Directed endpoint lookup returns the queue address.
- `GetTemporaryEndpointAddress(endpointName)` returns a response-queue address.
- `GetErrorAddress(endpointName)` returns the `<endpointName>_error` queue.
- `GetFaultAddress(endpointName)` returns the `<endpointName>_fault` topic.

## Topology Projection

The adapter projects `ReceiveEndpointTransportTopology` without adding
Azure-specific fields to the portable topology model.

For a durable endpoint named `orders` consuming message entity `order-submitted`,
the projection is:

1. Create the `orders` queue.
2. Create the `order-submitted` topic.
3. Create an `orders` subscription under that topic.
4. Configure the subscription to forward messages to the `orders` queue.
5. Create `orders_error` and `orders_skipped` queues.
6. Create the `orders_fault` topic when endpoint-specific fault publication is
   required.

Subscription names are scoped by topic, so the endpoint name is the stable
subscription name for every publish binding. A single endpoint consumes from
one queue even when it has several message bindings. This keeps competing
consumer and endpoint lifecycle behavior aligned with the RabbitMQ profile.

Transport options reserved for later typed projections include sessions,
duplicate detection, lock duration, maximum delivery count, auto-delete on
idle, and subscription rules. Unknown or unsupported options must fail startup
validation instead of being ignored.

## Message Mapping

The AMQP body contains the same UTF-8 MassTransit JSON envelope used by the
RabbitMQ profile. The content type defaults to
`application/vnd.masstransit+json`.

The adapter maps portable metadata to `ServiceBusMessage` or
`ServiceBusReceivedMessage` properties where a native property exists:

| Portable value | Azure Service Bus property |
| --- | --- |
| Message ID | `MessageId` |
| Correlation ID | `CorrelationId` |
| Content type | `ContentType` |
| Response address | `ReplyTo` |
| Time to live/expiration | `TimeToLive` |
| Remaining headers | Application properties |

Envelope fields remain authoritative for MassTransit wire compatibility.
Native properties support broker features, diagnostics, and non-MyServiceBus
peers; they do not replace the envelope.

Property conversion must use the intersection of AMQP application-property
types supported by the current .NET and Java SDKs. Unsupported values must be
converted by an explicit shared convention or rejected before send.

## Receive and Settlement

Receivers use peek-lock mode with automatic completion disabled.

- Complete the message after the consume pipeline finishes successfully.
- While an in-process retry policy is active, retain the same delivery and renew
  its lock as needed.
- If a transport operation or process failure prevents a terminal decision,
  abandon the message or allow its lock to expire so Service Bus can redeliver
  it.
- Do not complete an original delivery until any required compatibility copy
  has been accepted by its destination.

Prefetch maps to the native receiver prefetch option. Endpoint concurrency is a
separate concern and must not be inferred solely from prefetch.

## Failed and Skipped Messages

The initial profile deliberately uses MyServiceBus/MassTransit compatibility
destinations instead of treating the native dead-letter subqueue as the
application error queue.

- An exhausted consumer failure is copied to `<endpoint>_error` with the
  original body and metadata, then the original delivery is completed.
- An unrecognized message type is copied to `<endpoint>_skipped`, then the
  original delivery is completed.
- A consumer fault publishes the compatible `Fault<T>` envelope to the explicit
  fault address when present, otherwise to the endpoint's `<endpoint>_fault`
  topic according to the existing runtime policy.
- If copying to `_error` or `_skipped` fails, the original delivery is not
  completed. It is abandoned or allowed to unlock for another attempt.

The initial implementation does not claim an atomic transaction between the
compatibility send and completion. Duplicate compatibility messages are
therefore possible after an ambiguous network failure. This is an at-least-once
boundary and consumers of those destinations must be idempotent.

Native dead-letter queues remain available to Service Bus for broker-level
conditions such as maximum delivery count or expiration. A later profile option
may deliberately route application failures there, but it must not silently
replace the compatibility behavior.

## Request/Response and Temporary Endpoints

The factory produces the response address; portable request code never builds
an `sb` URI. In Azure, the adapter creates a uniquely named queue with an
appropriate auto-delete-on-idle lifetime and deletes it during orderly shutdown
when possible.

The emulator does not persist entities across restart and its management
surface differs from the cloud service. Emulator conformance tests therefore
use the pre-provisioned `msb-response` queue and run sequentially. This is a test
fixture constraint, not the production topology strategy.

## Topology Management Modes

The adapter should distinguish two explicit modes:

- `Create`: create or update required entities before receivers start. This is
  the normal cloud mode.
- `PreProvisioned`: do not mutate topology; connect to entities supplied by
  deployment infrastructure. Missing entities fail startup with a
  transport-specific topology exception.

Both C# and Java cloud adapters can use their platform administration SDKs.
The official emulator currently supports its administration endpoint natively
only through the .NET client, so shared emulator tests use `PreProvisioned`
mode and the checked-in JSON configuration.

## Emulator Conformance Boundary

The fixture under `test/AzureServiceBusEmulator` is intended for local and CI
data-plane tests. It uses a fixed AMQP port and static entity names, so suites
that share it must run sequentially and reset the Compose project between
scenarios that require empty entities.

The emulator is not sufficient evidence for every cloud behavior. Before the
transport is declared supported, an optional Azure smoke suite should cover at
least:

- cloud topology creation from both clients
- auto-delete response queues
- lock renewal during a long-running consumer
- `_error`/completion behavior across a transient failure
- MassTransit interoperability on the pinned compatibility version

Partitioned entities, networking and identity integration, geo-recovery,
metrics, quotas beyond the emulator limits, and production performance are
outside emulator conformance.

## Initial Conformance Matrix

The transport implementation is not complete until these scenarios pass for
both clients where applicable:

- C# queue send and receive
- Java queue send and receive
- C# publish consumed by Java
- Java publish consumed by C#
- correlated request and response in both directions
- envelope and native-property preservation in both directions
- retry success and retry exhaustion
- preservation in `_error` and `_skipped`
- endpoint-specific `Fault<T>` publication
- competing consumers receive one delivery once per attempt
- startup rejection for unsupported transport options
- C# and Java consumption of messages produced by the pinned MassTransit Azure
  Service Bus peer

Compatibility claims remain scoped to scenarios with executable evidence.
