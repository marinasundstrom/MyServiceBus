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

Language projections also act as design tests for the shared implementation.
For example, a Kotlin suspend-handler interface adds an intermediate generic
contract; supporting it requires the mediator to resolve inherited response
types correctly instead of assuming every Java handler implements the result
interface directly.

The first `SuspendHandler` experiment exposed that inheriting Java's
`handle(request): CompletableFuture<Response>` overload prevents Kotlin from
declaring the natural `suspend fun handle(request): Response` signature. The
shared JVM layer now carries only a `ResultHandler<Request, Response>` metadata
contract at that boundary. Java's `HandlerWithResult` and Kotlin's
`SuspendHandler` project their own execution shapes onto it.

## Current transition

`myservicebus-kotlin` is the first explicit projection. Its configuration DSL
uses composition around `BusRegistrationConfigurator`; its application-facing
messaging contracts compose over the Java bus, mediator, and scoped endpoints;
and its coroutine bridge maps suspension, completion, failure, and cancellation
onto the existing JVM pipeline. The DSL and top-level facades provide `jvm {
... }` only as an escape hatch for capabilities that do not yet have a
Kotlin-native projection.

This keeps ordinary Kotlin code independent from Java overloads while the
shared implementation still resides in the Java modules.

## Possible future module shape

If the projections grow independently, the JVM artifacts may be reorganized
around an implementation-focused shared core with separate Java and Kotlin
public projections. That decision should be driven by concrete pressure rather
than package symmetry. Useful triggers include:

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
pipelines.
