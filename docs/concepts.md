# Core Concepts

This project shares a common set of messaging concepts between the .NET and Java implementations.

## Envelope
Wraps a message with transport and metadata fields such as identifiers, addresses and headers. Both the Java and C# clients serialize envelopes in the same JSON structure so messages can be exchanged across runtimes.

## Fault
Represents an error that occurred while processing a message. A `Fault<T>` carries the original message alongside a list of captured exception details. Consumers can deserialize faults produced by either runtime.

## Batch
A `Batch<T>` groups multiple messages so they can be delivered as a single payload. In the JSON envelope the `message` property contains the array of messages directly, matching MassTransit semantics. The Java implementation now mirrors the C# version by extending `ArrayList<T>` so batches serialize as plain JSON arrays.

