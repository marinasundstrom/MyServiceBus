# Serialization Architecture Proposal

## Status

Accepted; implementation in progress. The serializer, deserializer, factory, message-body, registry, and configurable JSON metadata paths are implemented. BSON remains a follow-up slice. Current configuration is documented in [Message serialization](../message-serialization.md).

## Recommendation

Treat serialization as a bidirectional wire-format subsystem rather than a single outbound serializer.

Define one cross-platform serialization model and project it into C# and Java. The shared model is:

1. A serializer factory owns one matching serializer/deserializer pair.
2. The serializer converts a typed send context into a message body and declares its content type.
3. The transport transmits that body and content type without knowing the encoding library.
4. The inbound registry selects a deserializer from normalized content type and, where required, bounded protocol headers.
5. The deserializer converts the received body and metadata into an inbound serializer context.
6. The inbound context exposes envelope metadata and materializes the declared consumer contract.

C# and Java implement this same model. Their public types are language projections of the same responsibilities, and their built-in formats use the same component boundaries and runtime flow.

The subsystem should have these responsibilities:

- a small, stable serializer contract
- a corresponding deserializer contract
- a serializer factory that creates the matched pair
- a format-neutral message-body contract between serializers and transports
- an internal registry of outbound serializers and inbound deserializers
- explicit content-type and protocol-profile selection
- a stable logical envelope model separated from its JSON or BSON encoding
- application-owned payload metadata and library-owned envelope metadata
- corresponding C# and Java stages with idiomatic platform integrations rather than mechanically identical public types
- explicit capability and validation boundaries for trimming, .NET Native AOT, and GraalVM Native Image

The default remains the MassTransit-compatible JSON envelope. Additional formats are registered without replacing the ability to receive the default format unless an endpoint explicitly restricts its accepted formats.

```text
send context
  -> selected outbound profile
  -> envelope writer + payload codec
  -> transport body and content type

transport body and headers
  -> inbound profile matcher
  -> selected deserializer
  -> logical inbound message
  -> typed payload materialization
  -> consumer pipeline
```

The first compatibility extension should be the MassTransit BSON envelope profile. The first optimization extension should be configurable `System.Text.Json` metadata, including application-provided source-generated contexts.

These responsibilities do not require a public interface for every box in the architecture. The serializer, deserializer, factory, body, and inbound-context contracts are the useful boxes. Registry entries, matchers, codecs, and profile composition remain internal.

This follows the recognizable MassTransit structure without requiring its .NET implementation details. For the current design horizon, the shared model and C# projection should stay close to MassTransit's contracts, and Java should project that same model rather than define a parallel one. A serializer implementation is an adapter from MyServiceBus send semantics to one wire format. Whether that adapter uses `System.Text.Json`, Newtonsoft BSON, Jackson, a generated codec, or reflection is private to the implementation and its platform-specific configuration.

## Motivation

The current C# `IMessageSerializer` selects outbound content type and produces bytes, but inbound selection is a fixed switch inside `InboundMessageResolver`. Java separates `MessageSerializer` and `MessageDeserializer`, but its default resolver still hard-codes the recognized JSON profiles and only injects the envelope deserializer. Consequently:

- adding an outbound serializer does not add a matching inbound format
- an endpoint cannot naturally accept several envelope encodings
- NServiceBus detection is a resolver special case rather than a registered protocol profile
- JSON parsing and message materialization create serializer options internally
- source-generated payload metadata cannot flow through the built-in C# receive path
- the outbound serializer's `EnvelopeMode` is used to infer inbound raw-message dispatch
- each new format requires editing central resolver code in both clients

Adding a BSON case to those switches would reproduce the limitation rather than establish a durable extension model.

## Goals

- Preserve the default MassTransit JSON envelope and missing-content-type behavior.
- Support multiple simultaneously registered inbound formats.
- Select outbound and inbound formats independently.
- Make the MassTransit BSON envelope interoperable across MassTransit, C#, and Java.
- Let .NET applications supply source-generated JSON metadata without implementing the complete envelope protocol.
- Keep application message metadata separate from generated consumer discovery.
- Provide a strict, testable no-reflection path for .NET Native AOT.
- Provide a corresponding explicit-metadata path for Java native images.
- Keep broker transports independent of concrete JSON, BSON, Jackson, or `System.Text.Json` types.
- Keep portable serializer contracts free of serializer-library-specific metadata and mapper types.
- Permit later allocation and buffering improvements without another public configuration redesign.
- Reject unsupported or ambiguous serialization configuration at startup when possible.

## Non-Goals

- Making every optional serializer Native-AOT-compatible in its first release.
- Treating all valid BSON encodings as MassTransit-compatible.
- Inferring message identity from CLR or Java runtime type names embedded in a payload.
- Adding schema-registry, schema-evolution, compression, or encryption behavior in the first slice.
- Coupling the consumer generator or Java annotation processor to one serialization library.
- Automatically accepting arbitrary content types supplied by untrusted extensions.
- Replacing the transport header-convention abstraction.

## Architectural Model

### 1. Logical message envelope

MyServiceBus owns one format-neutral logical envelope containing the existing portable fields:

- message, request, correlation, conversation, and initiator identifiers
- source, destination, response, and fault addresses
- message type identities
- sent time
- application headers
- host information
- payload

The logical model is not itself a required in-memory object graph. A profile may write fields directly to an output writer or expose them lazily from an inbound body. It is the semantic contract shared by the encodings.

Envelope semantics and field names remain governed by the compatibility specification and conformance fixtures. A serializer profile cannot silently redefine identifier formats, address conventions, message URNs, or header precedence.

### 2. Serialization profile

A serialization profile is a design concept that bundles a coherent wire protocol. It is represented publicly by a clean serializer factory and its serializer/deserializer pair rather than a large profile interface. Conceptually, a registered format supplies:

- a stable profile identity for diagnostics and topology description
- one outbound content type
- one or more accepted inbound content types or protocol matchers
- an outbound serializer factory
- an inbound deserializer factory
- internal envelope mode and message-identity behavior
- optional compatibility and runtime-capability metadata

Examples are:

- MassTransit JSON envelope
- MassTransit BSON envelope
- neutral Raw JSON
- NServiceBus JSON over the documented RabbitMQ boundary

`application/json` alone cannot distinguish neutral Raw JSON from the NServiceBus profile. A profile matcher may therefore inspect a bounded set of transport headers in addition to the normalized content type. Match priority must be deterministic:

1. an exact registered protocol matcher, such as the required NServiceBus headers
2. an exact normalized content-type match
3. the endpoint's configured missing-content-type default
4. a serialization error when no profile matches

Profiles must reject ambiguous registration at bus construction. Runtime order of service registration must not decide which protocol consumes a message.

Format packages should normally expose configuration methods or builders that register their serializer factory. This avoids making applications implement a large profile abstraction and leaves room for C# delegates and extension methods or Java builders and factories where those are more natural.

### 3. Serializer and deserializer registry

Outbound and inbound selection are related but independent:

- The bus has one default outbound serializer.
- A send operation or endpoint may select another registered outbound serializer.
- A receive endpoint accepts a set of registered inbound deserializers.
- An endpoint may restrict that set for a protocol boundary.
- One inbound profile is the fallback only when transport content type is absent.

Selecting BSON for outbound messages should not remove JSON deserialization. Adding BSON only for migration should not change outbound JSON. This mirrors the useful distinction between adding a deserializer and selecting a serializer.

The registry is created during bus construction and becomes immutable before transports start. Receive transports depend only on the format-neutral inbound resolver; send transports receive a selected serializer through the existing context path.

The registry may remain an implementation detail. Its public configuration surface should express user intentions such as add, select, accept, or restrict rather than expose storage and matching mechanics.

### 4. Clean serializer boxes

The public contracts should contain only behavior needed by the runtime. C# and Java expose the same model with closely aligned names.

Apply this alignment rule:

1. Start from the current MassTransit contract and vocabulary for the C# client.
2. Carry the same contract responsibilities and component boundaries into Java.
3. Adjust only what Java's type system, exception model, naming conventions, or standard-library types require.
4. Keep Jackson, `System.Text.Json`, BSON-library, reflection, and generated-metadata configuration on concrete implementations or registration APIs.
5. Document any structural divergence and its reason in the C#↔Java parity matrix.

The default expectation is one model. Idiomatic implementation does not mean independently redesigning the public serializer architecture on each platform.

#### Message serializer

The serializer owns outbound encoding:

- stable content type
- convert a typed send context into a format-neutral message body

This should move the C# contract away from `Task<byte[]>` toward the MassTransit-style `GetMessageBody<T>(...)` shape. Serialization is not inherently asynchronous, and the body can decide when or how bytes are materialized. Use `GetMessageBody<T>` in C# and `getMessageBody` in Java so the central operation is recognizably aligned across MassTransit and both MyServiceBus clients. Java retains its own generic and checked-exception conventions around that operation.

Keep `EnvelopeMode` out of the MassTransit-compatible serializer interface. MyServiceBus currently exposes raw dispatch as optional `IMessageSerializerMetadata`/`MessageSerializerMetadata`; a ported envelope serializer therefore implements only the base contract, while a raw serializer may opt into the additional dispatch metadata.

#### Message deserializer

The deserializer owns inbound decoding:

- stable accepted content type
- convert a message body plus portable receive metadata into a format-neutral inbound/serializer context
- materialize messages using the implementation's configured type system and metadata strategy

The C# client needs this contract. The Java `MessageDeserializer` should evolve from an envelope-specific `<T> Envelope<T>` decoder into the same whole-format responsibility. JSON-envelope parsing then belongs to the JSON deserializer, BSON-envelope parsing to the BSON deserializer, and raw payload parsing to the raw deserializer.

#### Serializer factory

A serializer factory owns one coherent pair:

- stable content type
- create the serializer
- create the deserializer

This is the public unit registered by built-in and third-party format integrations. It follows MassTransit's useful `ISerializerFactory` boundary and prevents configuration from accidentally pairing a BSON serializer with a JSON deserializer. Dependency-injection-aware registration may create the factory through a C# delegate or Java provider, but the factory contract itself does not depend on a container.

The target contracts are intentionally small. In illustrative C# form:

```csharp
public interface IMessageSerializer
{
    string ContentType { get; }
    MessageBody GetMessageBody<T>(SendContext<T> context) where T : class;
}

public interface IMessageDeserializer
{
    string ContentType { get; }
    IInboundMessage Deserialize(ReceiveContext context);
}

public interface ISerializerFactory
{
    string ContentType { get; }
    IMessageSerializer CreateSerializer();
    IMessageDeserializer CreateDeserializer();
}
```

Java should expose the corresponding three interfaces and operations, changing casing, type tokens where required by deserialization, and checked exceptions only where Java requires it. Concrete constructors and configuration APIs remain platform-specific.

#### Message body

A message body separates serialization from broker APIs and eager `byte[]` allocation. It should provide the body length when known and stream, bytes, and string materialization. C# can closely follow MassTransit's `Length`, `GetStream`, `GetBytes`, and `GetString` contract. Java should expose corresponding `getLength`, `getStream`, `getBytes`, and `getString` operations using `InputStream` and Java nullability conventions. Concrete bodies may materialize eagerly, lazily, or from cached bytes.

#### Inbound serializer context

The existing `IInboundMessage`/`InboundMessage` concept is the natural counterpart to MassTransit's `SerializerContext`. It exposes normalized envelope metadata and typed message materialization. The names may remain MyServiceBus-specific, but the responsibility belongs to the serialization box rather than the transport or consumer pipeline.

The contracts should not expose:

- `JsonSerializerOptions`, `JsonSerializerContext`, or `JsonTypeInfo`
- Jackson `ObjectMapper`, modules, or `JavaType`
- BSON-library readers, writers, documents, or registries
- dependency-injection container types
- profile-matching collections or mutable registry state
- broker-specific message types

Platform-specific configuration belongs on the concrete serializer implementation or its registration extensions. For example, the .NET JSON implementation can accept `JsonSerializerOptions`, `IJsonTypeInfoResolver`, or `JsonSerializerContext`, while the Java JSON implementation can accept an `ObjectMapper`, modules, type tokens, or mapper customizers. Those details stay inside the adapter box even though they produce the same wire behavior.

C# and Java should align on names, responsibilities, and implementation stages: serializer, deserializer, serializer factory, message body, content type, get message body, deserialize, and inbound message. Differences should be limited to language or library constraints such as checked exceptions, runtime type tokens, stream types, and construction idioms.

### 5. Cross-language implementation alignment

Each built-in format should have corresponding implementation components and the same runtime flow in C# and Java:

```text
format registration
  -> serializer factory
  -> outbound serializer -> message body -> transport
  -> inbound deserializer -> inbound context -> consumer materialization
```

For example, the MassTransit JSON implementation should have a corresponding factory, serializer, message body, deserializer, and inbound context in both clients. Raw JSON, NServiceBus JSON, and BSON follow that same composition. A format must not be implemented as a serializer/deserializer pair in one client but as special cases in a central resolver in the other.

The implementation classes should correspond closely in responsibility and lifetime even when their internals differ:

- C# may resolve `JsonTypeInfo`, use generic type reification, and select reflection or source-generated metadata.
- Java may carry `Type`/`JavaType`, use Jackson modules, and require explicit native-image reachability configuration.
- C# and Java may use different BSON libraries.
- Both still build the same logical envelope, select the same content type, merge headers with the same precedence, materialize the same declared contract, and surface equivalent errors.

Shared fixtures and scenario tests validate this alignment. Similar class structure is a design aid, while wire behavior and runtime stage correspondence are the acceptance boundary.

### 6. Payload codec boundary

An envelope profile owns wire structure, content type, protocol headers, and envelope rules. Payload metadata is supplied separately.

For the default C# JSON profile:

- MyServiceBus supplies generated metadata or direct writer logic for its envelope fields.
- The application supplies `JsonTypeInfo` metadata for application messages.
- The profile writes the `message` property using the resolved metadata for the declared message contract.
- The inbound context uses the same resolver to materialize the requested consumer type.

This avoids requiring the application to register every closed `Envelope<T>` merely to source-generate `T`. It also avoids asking a MyServiceBus generator to become the authority for application JSON policy.

The same conceptual boundary applies in Java even though Jackson, build-time modules, and native-image reachability use different APIs. Cross-language parity is measured at the envelope and payload behavior boundaries, not by requiring identical metadata interfaces.

### 7. Inbound message ownership

An inbound deserializer returns a format-neutral inbound message/context that exposes envelope metadata and can materialize a requested payload type. It may parse eagerly or lazily, but it must:

- own and release any pooled buffers or parsed document resources
- cache successful typed materialization per contract type
- preserve the original body for error transport and diagnostics
- distinguish missing message data from malformed data
- surface a typed serialization exception rather than convert every failure to `false`
- enforce configured body, nesting, string, collection, and document limits

The consumer pipeline should not know whether metadata came from JSON, BSON, or transport headers.

## Proposed Configuration Semantics

The serializer factory is the public grouping contract; the registry and match rules remain internal. The intended C# experience is structurally similar to:

```csharp
services.AddServiceBus(bus =>
{
    bus.Serialization(serialization =>
    {
        serialization.UseSystemTextJson(options =>
        {
            options.TypeInfoResolverChain.Insert(0, ApplicationJsonContext.Default);
        });

        serialization.AddMassTransitBson();
    });
});
```

The corresponding Java experience should preserve the same intentions while using factories and configured mapper/codec instances rather than classpath scanning:

```java
services.from(MessageBusServices.class)
        .addServiceBus(bus -> {
            bus.serialization(serialization -> {
                serialization.useJacksonJson(applicationMapper);
                serialization.addMassTransitBson(bsonConfiguration);
            });
        });
```

The design must also support these separate intentions:

- add an inbound deserializer without changing outbound serialization
- select a registered outbound serializer globally
- select an outbound serializer for one endpoint or send operation
- restrict one endpoint to a protocol profile
- explicitly choose the fallback for messages without content type
- clear defaults only through an explicit advanced operation

The pre-release `SetSerializer` and Java serializer/deserializer class setters are replaced by the factory-based `AddSerializer`/`AddDeserializer` model. `ClearSerialization` is the explicit advanced operation for removing defaults.

The examples deliberately align the configuration story without requiring the nested configuration types or serializer-library options to implement corresponding public interfaces.

## System.Text.Json and Source Generation

### Metadata ownership

MyServiceBus should publish an internal `JsonSerializerContext` for library-owned types and accept one or more application resolvers through `JsonSerializerOptions.TypeInfoResolverChain` or an equivalent configuration overload.

The application owns metadata for:

- application message contracts
- application-defined header value types that it elects to serialize
- custom converters and naming policy within the supported compatibility boundary

MyServiceBus owns metadata for:

- envelope infrastructure
- host information
- portable fault and exception contracts
- built-in batch and request/response infrastructure where applicable

Compatibility-sensitive envelope property names and shapes cannot be changed through application naming policy. Payload policy may be configurable where it does not break the selected interoperability profile.

### Strict mode

Provide an explicit strict mode suitable for CI and AOT preparation. In strict mode:

- no reflection-based `JsonTypeInfoResolver` is appended
- missing metadata fails with a domain-specific error naming the contract and profile
- known registered receive contracts are validated before endpoints start
- factories and serializer instances are used instead of reflective activation
- trim and dynamic-code annotations accurately describe compatibility convenience APIs

Applications should be able to run strict mode on CoreCLR. This exposes missing metadata before a Native AOT publish.

### Generator relationship

The consumer generator and Java annotation processor may contribute a language-neutral message-contract catalog for startup validation. They must not become the sole serialization authority because they cannot reliably discover:

- sent-only or published-only message types
- types selected by generic application code
- external contracts registered by another module
- every closed fault, batch, or request type
- application-specific converter policy

An application may choose to generate both its consumer catalog and JSON context from the same declarations, but the two artifacts remain independently replaceable.

## MassTransit BSON Profile

### Compatibility target

The target is the BSON envelope produced and consumed by MassTransit's Newtonsoft BSON serializer, with content type:

```text
application/vnd.masstransit+bson
```

This is an optional MassTransit compatibility profile, not a generic claim that every BSON producer is accepted.

The complete envelope is BSON-encoded. The profile must match MassTransit behavior for:

- camel-case envelope property names
- absent, null, and default values
- UTC and offset date/time values
- GUID representation
- integral, floating-point, and decimal values
- binary data
- arrays, maps, and nested documents
- message URNs and address strings
- header values supported by the interoperability contract
- disabled runtime type-name emission

### Packaging

BSON dependencies should live in optional C# and Java packages/modules. The portable abstractions and default JSON path must not depend on a BSON library.

The first .NET implementation may use the same Newtonsoft BSON stack as MassTransit to reduce compatibility risk. The Java implementation may use a different BSON library, but compatibility is accepted only through fixtures and live interoperation, not library branding.

Each package declares its supported runtime modes. BSON may initially be supported on managed .NET and the Java JVM without claiming Native AOT or native-image support. That limitation must not weaken the source-generated JSON path.

### Conformance evidence

Check in binary fixtures together with a readable manifest describing their contracts and expected logical envelope values. Cover:

- MassTransit to C# and Java
- C# and Java to MassTransit
- C# to Java and Java to C#
- send, publish, request, response, and fault envelopes
- batches and nested contracts
- application and transport headers
- null/default handling
- timestamp and GUID boundaries
- numeric-width and decimal boundaries
- malformed documents and configured limits

Byte-for-byte equality is useful for canonical fixtures where the writer is deterministic, but successful logical cross-decoding is the compatibility requirement when BSON field order is not semantically significant.

## Performance and Body Abstraction

The current `Task<byte[]>` boundary forces a complete allocation even when serialization is synchronous. A `MessageBody` return boundary lets each serializer control eager or lazy materialization without committing every implementation to streaming immediately.

The portable conceptual result is a message body with:

- known or optional length
- read-only bytes when materialized
- a stream or writer path where the transport supports it
- clear ownership and disposal rules
- optional cached materialization for retry or multiple transport reads

An idiomatic C# implementation may use `ReadOnlyMemory<byte>`, `IBufferWriter<byte>`, pooled buffers, and `ValueTask`. Java may use `byte[]`, `ByteBuffer`, or an `OutputStream`/channel adapter according to the broker client. Public parity is behavioral rather than a mechanical type translation.

Optimization work should be benchmark-driven and separated into:

- cold-start metadata construction
- first-message serialization and deserialization
- steady-state throughput
- bytes and objects allocated per message
- retained memory for lazy inbound contexts
- executable size and startup for native builds
- broker-backed end-to-end throughput and latency

Source generation, Native AOT, pooling, and BSON are separate variables in those measurements.

The published comparison matrix must show the .NET default reflective
`System.Text.Json` path and the application-supplied source-generated metadata
path as separate rows. At minimum, record startup, first serialize/deserialize,
steady-state throughput, allocation per operation, and Native AOT published
size. Do not merge source-generation gains with pooling or transport changes in
the same comparison.

## Error Model and Safety

Add domain-specific serialization exceptions with the original exception as `InnerException`/cause. Diagnostics should identify:

- serialization or deserialization direction
- selected profile and normalized content type
- declared message contract when known
- endpoint when available
- whether metadata was missing, the document was malformed, or a configured limit was exceeded

Do not include the message body, arbitrary headers, credentials, or application secrets in exception messages by default.

All profiles must enforce bounded parsing. A profile must not enable polymorphic runtime type-name loading from untrusted input. Duplicate BSON/JSON properties, invalid document lengths, unsupported BSON types, invalid addresses, and unrepresentable header values need explicit conformance behavior.

## Runtime Capability Model

Documentation and startup diagnostics should distinguish these capabilities rather than use one broad `AotCompatible` claim:

- reflection-free activation
- reflection-free contract metadata
- trim safety
- no dynamic-code generation
- .NET Native AOT verified
- GraalVM Native Image verified
- cross-language wire compatibility verified
- MassTransit interoperability verified

An initial expected matrix is:

| Profile | Managed/JVM | .NET Native AOT | Java native image | MassTransit wire profile |
| --- | --- | --- | --- | --- |
| MassTransit JSON with generated metadata | Yes | Primary target | Primary target | Yes |
| MassTransit JSON with reflective metadata | Yes | No | Requires reachability configuration | Yes |
| MassTransit BSON | Optional | Not initially claimed | Not initially claimed | Yes |
| Raw JSON | Yes | With complete metadata | With complete metadata | No peer-specific profile |
| NServiceBus JSON | Yes | With complete metadata | With complete metadata | No; separate NServiceBus profile |

The matrix records verified combinations; it is not a permanent restriction on later BSON AOT work.

## Transport and Endpoint Integration

Transport adapters continue to map the selected serializer content type to their native content-type property or header convention. They must not instantiate a JSON or BSON reader.

Receive transports pass body and headers to the registry-backed inbound resolver. Missing content type continues to mean the default MassTransit JSON envelope unless an endpoint explicitly configures another fallback. Unknown content types fail through the normal error-transport path with the original body preserved.

Responses, faults, and follow-up operations need an explicit policy. The initial policy should preserve current behavior: they use the endpoint's configured outbound serializer. Automatically mirroring the inbound format should be considered separately because it can make an endpoint's output nondeterministic and may select an untrusted or receive-only profile.

## Delivery Plan

### Slice 1: Registry foundation

- Evolve `IMessageSerializer`/`MessageSerializer` to return a message body rather than an asynchronously wrapped byte array.
- Remove outbound `EnvelopeMode` from the base serializer contract; keep MyServiceBus-specific raw dispatch behavior in optional metadata until endpoint format descriptors replace it.
- Add corresponding message-body, whole-format deserializer, and serializer-factory contracts in C# and Java.
- Introduce the internal serializer/deserializer registry keyed by normalized content type and bounded protocol matchers.
- Adapt existing MassTransit JSON, Raw JSON, and NServiceBus JSON behavior.
- Inject the registry-backed inbound resolver into every receive and request path.
- Preserve wire defaults while replacing the pre-release class setters with MassTransit-shaped factory registration.
- Add ambiguity and missing-content-type tests.

No new wire format ships in this slice.

### Slice 2: Configurable JSON metadata

- Add application-provided `System.Text.Json` resolver/context configuration.
- Add library-owned generated envelope metadata or direct envelope writer logic.
- Route inbound payload materialization through the configured metadata.
- Add strict reflection-disabled validation and a Native AOT smoke using the built-in profile.
- Add the corresponding explicit Jackson/native-image metadata path and smoke coverage.

### Slice 3: MassTransit BSON

- Add optional C# and Java BSON modules.
- Register BSON as an additional inbound profile without changing the default outbound profile.
- Add shared binary fixtures and live MassTransit RabbitMQ tests in both directions.
- Publish the verified runtime capability boundary.

### Slice 4: Body and allocation optimization

- Optimize message-body implementations and transport materialization paths.
- Remove avoidable completed-task and byte-array allocations.
- Establish ownership for parsed documents and pooled buffers.
- Benchmark before and after on managed, Native AOT, JVM, and native-image paths.

### Slice 5: Stability decision

- Stabilize the serializer, deserializer, factory, body, and inbound-context boxes; keep registry and profile-matching mechanics internal.
- Record compatibility/versioning rules for content types and profile identities.
- Update the C#↔Java parity matrix and public feature walkthrough.
- Consider schema-registry, compression, or encryption layers only as separate proposals.

## Acceptance Criteria

The architecture is ready for stabilization when:

- C# and Java can register more than one inbound envelope encoding through corresponding serializer factories without central resolver changes or a large public profile interface.
- Existing JSON, Raw JSON, and NServiceBus scenarios retain their documented behavior.
- a .NET application can use the built-in JSON envelope with an application `JsonSerializerContext` and reflection disabled
- the equivalent Java native-image smoke uses explicit application contract metadata
- BSON passes the complete C#↔Java↔MassTransit conformance matrix
- transports contain no concrete JSON or BSON parsing logic
- missing metadata, unsupported content types, malformed bodies, and limit violations use actionable domain-specific errors
- benchmarks and native executable smokes verify the capability claims made in documentation

## Decisions Recorded by This Proposal

- Serialization is bidirectional and registry-based internally; serializer factories are the public pairing boundary, not a public registry or large profile interface.
- C# and Java are projections of one serialization model: factory, serializer, message body, deserializer, inbound context, resolver, and transport handoff.
- Outbound selection does not determine the complete accepted inbound set.
- The envelope protocol and application payload metadata have separate owners.
- Source-generated JSON remains application-configurable rather than consumer-generator-owned.
- BSON means the explicit MassTransit BSON envelope profile.
- BSON is optional and may have a narrower initial AOT capability than JSON.
- Responses initially use the endpoint's configured outbound profile rather than mirroring arbitrary inbound content types.
- Optimization follows the architectural seam and measured evidence; Native AOT is not used as a synonym for general performance.
- C# and Java serializer APIs align semantically while retaining idiomatic configuration, async, buffer, and exception choices.
- Built-in serializer implementations use corresponding components and runtime stages in C# and Java; platform differences remain inside those components.
- Concrete serializers are platform adapters; their reflection, generated metadata, Jackson, `System.Text.Json`, or BSON machinery is not part of the portable contract.
- For the current design horizon, C# serializer contracts remain close to MassTransit and Java serializer contracts remain close to C#; structural divergence requires an explicit platform reason and parity documentation.

## Open Questions

- Should endpoint accepted-profile restrictions be allowlists, a single named protocol boundary, or both?
- Which application header value types form the portable JSON/BSON compatibility set?
- Should strict metadata validation include only receive contracts or also an explicit catalog of send/publish contracts?
- Which BSON library gives the Java client the smallest dependable MassTransit compatibility surface?
