# AOT proof-of-concept benchmarks

The committed harnesses compare reflection-based and typed registration, same-application reflection and generated catalogs, and generated consumer-method dispatch. They are microbenchmarks for repeatable development measurements, not broker-backed capacity or whole-process startup claims.

Run the .NET BenchmarkDotNet suite from the repository root:

```bash
dotnet run -c Release --project benchmarks/MyServiceBus.Benchmarks
```

Build and run the Java JMH suite:

```bash
gradle :myservicebus-benchmarks:jar
java -jar src/Java/myservicebus-benchmarks/build/libs/myservicebus-benchmarks-0.1.0-preview.11.jar
```

The Java native smoke application accepts `--benchmark` for the identical generated-catalog mediator workload used to compare GraalVM JIT and Native Image execution. The supported end-to-end native compilation check is:

```bash
./eng/verify-java-aot.sh
```

The catalog benchmarks register the same interface consumer and attributed method consumer through each discovery path. This isolates registration-phase work performed during application startup without presenting it as total startup time. Current measurements and their limitations are recorded in `docs/development/native-aot.md` and the website AOT page. Process startup, memory measurements, and broker-backed throughput remain separate measurements.

## JSON serialization metadata

Run the reflective-versus-source-generated .NET comparison with:

```bash
dotnet run -c Release --project benchmarks/MyServiceBus.Benchmarks -- --filter '*JsonSerializationBenchmarks*'
```

The harness reports separate envelope/raw serialize and deserialize groups with memory diagnostics. Keep the reflective methods as each group's baseline. The published comparison matrix should additionally record cold first-use latency, process startup, and Native AOT published size from dedicated process-level runs; BenchmarkDotNet's warmed steady-state results do not represent those measurements.

## .NET 11 Runtime Async

.NET 11 Runtime Async is a separate performance axis from generated registration. Registration benchmarks are synchronous and remain on the supported .NET 10 baseline. Async dispatch measurements should eventually compare .NET 11 builds with Runtime Async enabled and disabled so runtime improvements are not attributed to consumer generation.

The experimental NativeAOT smoke compiles an `IConsumer<TMessage>` implementation with `runtime-async=on`, forces a real suspension with `Task.Yield()`, resumes it through generated registration, and verifies the result:

```bash
./eng/verify-dotnet11-runtime-async-aot.sh
```

The smoke sets `EnableNet11RuntimeAsyncTarget=true` so the core abstractions and mediator runtime are recompiled for `net11.0` with Runtime Async, rather than testing only a .NET 11 application over .NET 10 library binaries. The opt-in target is pinned to a .NET 11 preview SDK until the November 2026 stable release and is not emitted by normal package builds. Runtime Async is not required for NativeAOT: compiler-generated async state machines remain AOT-compilable on .NET 10.
