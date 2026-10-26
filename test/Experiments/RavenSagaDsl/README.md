# Raven saga macro prototype

This experiment proves that a Raven declaration macro can lower a compact `saga!` state machine into the ordinary MyServiceBus .NET saga API. The generated type derives from `SagaStateMachine<TSaga>`, produces the same normalized definition, and executes through `SagaStateMachineRuntime<TSaga>` with an ordinary `InMemorySagaRepository<TSaga>`.

The sample deliberately targets the native version 1 saga profile. It supports:

- a generated saga-data class;
- identity correlation, with an event handled in `initially` inferred to create the instance;
- named states and `initially` / `in State` / `on Message` behaviors;
- mutation, convention-based sends, publications, transitions, and finalization; and
- compile-time diagnostics for malformed or currently unsupported constructs.

## Language and tooling boundary

The prototype treats the saga body as a precise domain language rather than a
block of arbitrary Raven code:

- `on` completes only correlated message declarations;
- `in` and `transition` complete only states declared by this saga;
- `set` completes only declared saga-data members;
- behavior positions complete only supported activities; and
- correlation message positions offer compiler symbols matching the typed
  prefix.

Raven owns the deliberately embedded expression positions. The right-hand side
of `set`, plus the complete message-construction expression after `send` or
`publish`, are reported as Raven expression fragments. Inside a behavior the
handler alias, normally `message`, is introduced with the correlated message
type, so ordinary Raven diagnostics, hover, member completion, and navigation
can operate there. Everything around those fragments remains owned and
validated by the saga grammar.

This split is intentional: the DSL determines which operation, state, message,
and saga member is legal; Raven expressions determine only the value being
assigned or the outgoing message being constructed.

It does not silently approximate the fuller proposal. Named final outcomes, guarded transitions, and durable `after` scheduling remain unsupported until the shared MyServiceBus primitives define their behavior. The macro reports those gaps instead of generating a different runtime contract.

## Compiler upgrade

Keep `Raven.Sdk` and `Raven.CodeAnalysis` on 0.1.16 and rebuild both projects together. The [Raven changelog](https://github.com/marinasundstrom/raven/blob/v0.1.16/CHANGELOG.md) records compiler API changes since 0.1.6, including removal of `TypeSymbolExtensions.GetClrType`. This provider uses semantic symbols and explicit `IMacroDefinition` implementation, so neither that removed adapter nor the new synthesized `NameMacro` naming requires a source migration here.

The 0.1.16 `Option` → `Optional` and `Result.Error` → `Result.Failure` renames do not apply to the prototype's own `Error` properties or diagnostic helpers. Keep those names. Compact macro declarations and newer editor support remain possible tooling improvements; they do not expand the saga runtime's supported semantics.

## Run

The projects use the published Raven 0.1.16 SDK and compiler API, while referencing the local MyServiceBus source project. The published compiler host requires the .NET 11 RC3 runtime (`11.0.0-rc.3.26522.148`), even though these projects target `net10.0`. Install that runtime alongside the repository SDK before running:

```bash
dotnet run --project test/Experiments/RavenSagaDsl/app/RavenSagaDslSample.rvnproj --property WarningLevel=0
```

Expected output:

```text
Raven saga macro sample passed through the MyServiceBus runtime.
```

The syntax in `app/src/Program.rvn` is a prototype, not a stable language contract. In particular, `send Message(...)` currently uses the portable sample convention `queue:Message`; a production DSL needs an explicit or topology-resolved destination model.

The experiment also found that Raven treats a CLR member named `Finalize` as a
special member name. The C# binder now exposes `FinalizeSaga`, matching Java's
existing `finalizeSaga`, and the macro lowers its `finalize` statement to that
unambiguous primitive.
