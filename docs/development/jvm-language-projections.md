# JVM language projections

MyServiceBus currently implements its JVM runtime in Java and layers the
`myservicebus-kotlin` package on top. Kotlin is a main target, not a convenience
wrapper, so the Java public API must not dictate Kotlin method and class shapes.

## Boundary

The shared JVM layer should own behavior that must remain identical:

- transport adapters and broker lifecycle;
- topology, endpoint naming, and message bindings;
- envelopes, serialization, retry, fault, and settlement semantics;
- scoped consumer execution and cancellation signals;
- inspection and monitoring contracts.

Language projections should own source-level experience:

- Java can expose fluent configuration, class literals, functional interfaces,
  and `CompletableFuture`;
- Kotlin can expose receiver DSLs, reified types, suspending consumers,
  coroutine cancellation, and Kotlin-friendly nullability and defaults.

During the POC, Java is the stable reference projection. Kotlin may change its
contracts and syntax independently while the native shape is discovered, but
those experiments should lower through adapters rather than forcing breaking
changes into Java's consumer and configuration APIs.

The MVP release has two stability levels: the common JVM core and Java
projection are stabilized release surfaces, while the Kotlin projection is
explicitly experimental. Kotlin can evolve independently, but it must continue
to prove the same observable core behavior.

The public concept map should stay directly translatable across C#, Java, and
Kotlin: consumer, consume context, send context, publish context, definition,
endpoint, handler, request client, and bus should retain the same meaning and
observable behavior. Equivalent concepts do not require equivalent inheritance
or method signatures.

Java and Kotlin may share the `com.myservicebus` package root, but frontend
classes that can coexist cannot use the same fully qualified JVM name. Separate
artifacts do not change that classloader constraint. The current Java
`com.myservicebus` and Kotlin `com.myservicebus.kotlin` packages therefore let
both projections coexist and explicitly bridge consumers authored against the
other frontend. A future physical core split must preserve that coexistence;
using identical fully qualified names would instead make the projections
mutually exclusive.

Framework integration belongs at this projection boundary too. A Kotlin server
adapter should use the framework's lifecycle, configuration, dependency, and
coroutine conventions instead of exposing a Java bootstrap API through Kotlin.
The Ktor sample is the first proof: an application plugin owns bus startup and
shutdown, builds and owns the shared service provider from native configuration
blocks, and presents a Kotlin runtime with readiness and suspending scoped
access plus direct messaging operations to routes, while transport and delivery
remain in the shared JVM implementation. Keeping this adapter in the sample
first lets lifecycle and
scope ownership settle before a framework package becomes a compatibility
commitment.

Both projections must enter the same topology and pipeline stages. A Kotlin
consumer is not a parallel runtime path: its coroutine is adapted to the shared
consumer completion contract at the projection boundary.

Shared behavior does not require shared result syntax. Multiple-response
requests use a Java 17 sealed interface with nominal record cases in the Java
projection. Kotlin maps those cases to a covariant sealed result so callers get
an exhaustive `when`, reified response discovery, and coroutine cancellation.
The branch discriminator belongs below both projections because runtime type
inspection cannot distinguish identical or assignable alternatives reliably.

The same rule applies to operations. Kotlin extensions cannot reliably replace
Java members with the same name because member resolution wins before extension
resolution, and Java overload sets may be invalid or ambiguous from Kotlin.
When that happens, the Kotlin projection should own a small composition type
with the familiar domain name and adapt to the Java implementation internally.
`Consumer<T>` and `ConsumeContext<T>` are the first proof: Kotlin callers use
suspending `publish`, `send`, and `respond` members while the shared pipeline
continues to execute the Java context underneath. The POC deliberately extends
that approach across the entire application boundary: Kotlin owns facades for
`MessageBus`, `Mediator`, publish/send endpoints, and endpoint providers. This
lets the projection discover its native shape before Java compatibility
constraints are allowed to narrow it. Suffixes such as `Await` are transitional
for explicitly raw Java types, not the target Kotlin vocabulary.

Composition must preserve more than method names. For example,
`ConsumeContext.send(destination, message)` delegates through the shared consume
context rather than merely resolving and invoking a send endpoint, because the
context applies conversation, initiator, and causation metadata first. The
ordinary Kotlin endpoint-provider facade can resolve an endpoint directly. This
kind of distinction is why the Kotlin projection also serves as an architectural
test of what behavior belongs in a future shared JVM core.

Configuration callbacks follow the same boundary. Kotlin-owned `PublishContext`
and `SendContext` receivers expose the shared mutable metadata as properties and
adapt the completed configuration back into the exact Java context instance.
This keeps configuration source independent from Java accessor conventions
without copying state or creating a second outbound-message model. The first
extracted context capability is `OutgoingMessageContext`: the runtime
`SendContext` implements it, while the Kotlin send and publish contexts compose
over the capability instead of requiring that concrete Java class for their
ordinary properties.

Request/response follows the full-facade rule even though extension methods
could technically hide some class-token ceremony. Kotlin owns
`RequestClientFactory` and `RequestClient`, accepts Kotlin `Duration`, discovers
request and response classes through reified type parameters, and projects
multiple outcomes as `RequestResult`. The Java factory, client, timeout wrapper,
future overloads, and `Response2` stay behind that boundary.

Language projections also act as design tests for the shared implementation.
For example, a Kotlin suspend-handler interface adds an intermediate generic
contract; supporting it requires the mediator to resolve inherited response
types correctly instead of assuming every Java handler implements the result
interface directly.

The first handler experiment exposed that inheriting Java's
`handle(request): CompletableFuture<Response>` overload prevents Kotlin from
declaring a natural suspending contract. Kotlin therefore owns
`Handler<Request, Response>` and receives its Kotlin `ConsumeContext<Request>`.
Both Kotlin consumers and handlers normalize their identity, message contract,
and endpoint policy into `ConsumerDefinitionModel`, then adapt their execution
to the shared `ConsumerInvoker`. The runtime neither discovers nor invokes a
Kotlin method directly. Java interface consumers and Java consumer methods can
lower to the same registration primitive without becoming the superclass of
the Kotlin frontend.

The consume path now makes that boundary executable as well. The shared
`MessageDeliveryContext` carries incoming state and projection-neutral delivery
operations, while Java's `ConsumeContext` implements it without changing its
existing overloads. `ConsumerInvoker` receives the shared contract. Java's
`ConsumerMethodInvoker` remains a functional interface with its familiar
concrete `ConsumeContext` parameter and supplies the adapter itself, so generated
and handwritten Java registrations keep their source shape. Kotlin consumers
compose their own suspending `ConsumeContext` directly over the shared contract;
tests invoke one with no Java consume-context instance at all.

## Current transition

`myservicebus-kotlin` is the first explicit projection. Its configuration DSL
uses composition around `BusRegistrationConfigurator`; its application-facing
messaging contracts compose over the Java bus, mediator, and scoped endpoints;
and its coroutine bridge maps suspension, completion, failure, and cancellation
onto the existing JVM pipeline. The DSL and top-level facades provide `jvm {
... }` only as an escape hatch for capabilities that do not yet have a
Kotlin-native projection.

This keeps ordinary Kotlin code independent from Java overloads while the
shared implementation still resides in the Java modules. Java-authored
consumers remain available through the deliberately named `javaConsumer<T>()`
bridge; `consumer<T>()` is reserved for the Kotlin contract so overload
resolution cannot accidentally select the other frontend.

Lifecycle values are part of the projection as well. The Kotlin bus accepts
Kotlin `Duration` for bounded stop, treats `Duration.INFINITE` as the untimed
shared stop, and implements `AutoCloseable`; Java's `java.time.Duration` remains
behind the facade.

## Emerging module shape

The JVM implementation should evolve toward an implementation-focused shared
core with separate Java and Kotlin public projections. The normalized
`ConsumerRegistration` and `ConsumerDefinitionModel`, `ConsumerInvoker`, and
narrow `ConsumerRegistrationConfigurator` are the first executable logical
seam. Frontend-specific consumer and handler shapes become definitions and an
invocation adapter before topology is materialized, and Kotlin registration
depends on the narrow sink rather than Java's full bus configurator.

These types deliberately remain in the current implementation package during
the POC. We should establish the context, configuration, transport, and runtime
dependency boundaries through working projections before assigning classes to
new Maven artifacts or final JVM packages. The final Java/core/Kotlin package
and artifact graph must be settled before the Kotlin work is released.

Pressure that determines the next extraction boundary includes:

- Java API compatibility preventing an idiomatic Kotlin operation;
- Kotlin types or coroutine dependencies leaking into shared transport code;
- duplicated topology or pipeline behavior between language entry points;
- an inability to test the shared behavioral contract without a public Java
  facade.

A split must not duplicate transports or create different wire behavior. It
also must account for artifact migration, framework adapters, binary
compatibility, and staged consumers before published coordinates change.

## Verification

Every projection should verify the same observable behavior—topology,
delivery, failure, cancellation, scope lifetime, and wire representation—while
also testing its language-specific API contract. Kotlin tests therefore cover
both coroutine behavior and entry into the existing mediator and consumer
pipelines. Framework samples add an end-to-end gate over real lifecycle and
broker behavior; the Ktor gate covers health readiness, publish, directed send,
request/response, consumer completion, and graceful shutdown through RabbitMQ.
