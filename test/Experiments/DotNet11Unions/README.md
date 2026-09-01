# .NET 11 Union Prototype

This executable spike proves the C#/.NET projection proposed in
[Union-Typed Consumers](../../../docs/proposals/union-typed-consumers.md).
It is not a supported target or API commitment.

Run it from this directory so the nested `global.json` selects the pinned .NET 11 preview SDK:

```shell
dotnet run --project MyServiceBus.DotNet11UnionPrototype.csproj \
  -p:EnableNet11RuntimeAsyncTarget=true
```

To verify the public package surface rather than project references, run:

```shell
./verify-package-consumer.sh
```

The script packs the .NET 11 variants of `Sundstrom.MyServiceBus.Abstractions` and `Sundstrom.MyServiceBus` into an isolated temporary feed, restores separate C# and Raven apps through `PackageReference`, and runs their assertions against those packages. Neither consumer build uses unpublished project references.

The prototype verifies that:

- reflection discovery expands one C# union parameter into one registration per concrete message case;
- all cases share the declared endpoint;
- the union carrier does not become message topology or a wire contract;
- dispatch constructs the carrier locally and invokes one exhaustive C# handler;
- the existing C# `Response<T1, T2>` shape can implement the .NET union ABI; and
- `System.Text.Json` writes only the selected response case.

The Raven consumer is an attributed namespace-level function that accepts the named parenthesized union `union OrderCommand(SubmitOrder | CancelOrder)`. Raven emits the standard .NET union ABI for that declaration. This verifies both that consumer-method discovery recognizes Raven's compiler-generated CLR method and that union discovery is based on the shared CLR ABI rather than the C# compiler or a C# named-union type.

This is a C# convenience over ordinary MyServiceBus contracts. Java and older .NET applications continue to publish and consume `SubmitOrder`, `CancelOrder`, and response contracts normally. They do not need the C# carrier or union language support.

The reflection adapter caches a compiled constructor delegate per case. The C# source generator now emits direct case construction and registrations, and the .NET 11 Runtime Async NativeAOT smoke executes both generated case adapters without reflection or expression compilation. Raven source remains outside the C# generator, so trimmed or NativeAOT Raven consumers still require Raven-owned generation or a shared descriptor manifest. Both paths must produce the same concrete message descriptors and transport topology.
