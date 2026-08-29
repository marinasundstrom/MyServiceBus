# AGENTS Instructions

This repository contains a .NET solution for MyServiceBus and a Java project. Follow these guidelines when contributing:

## Code style
- Use standard C# conventions (PascalCase for types and methods, camelCase for locals and parameters).
- Run `dotnet format` only on source code files you create or modify to automatically format them; do not run it on Markdown or other non-code files.
 
## Exception handling
- Handle exceptions locally when you can do something useful with them.
- If a public API has notable exceptions that callers should know about, document them in XML docs.
- Declare and use domain-specific exceptions when no built-in type clearly conveys the problem, wrapping the original exception as the `InnerException` for context. Domain-specific exceptions are preferred when surfacing errors to API consumers in a meaningful way.

## Testing
- Run the narrowest relevant test projects before committing. For shared infrastructure such as serialization, run the core tests and relevant local broker tests; a full solution test is not required for every slice.
- Run Azure Service Bus tests only when Azure transport code, Azure-specific mapping, or an explicitly Azure-facing behavior changes. Do not run them routinely for shared serializer, registry, or contract work.
- Reserve the full `dotnet test` solution pass for release validation, broad cross-cutting changes, or when the affected boundary cannot be isolated confidently.
- If your changes only affect documentation (e.g., Markdown files or other non-code assets), you may skip running build or test steps.
- When adding features or changing API/behavior, implement them for both the C# and Java codebases.
- Create or update tests for each language to cover new functionality.

## Documentation
- Write documentation in Markdown and place files in the `docs/` folder when appropriate.
- See `docs/development/design-goals.md` for overarching design goals, including MassTransit familiarity and C#↔Java parity.
- Review `docs/specs/myservicebus-spec.md`, `docs/development/design-guidelines.md`, and related design documents for solution details; keep them and other docs up to date.
- `docs/feature-walkthrough.md` is the canonical source for usage samples of MyServiceBus.
- Keep `CHANGELOG.md` up to date for significant repository changes. Prefer chronological entries that summarize the larger themes of a change set rather than exhaustive commit-by-commit notes.

## Deployable artifacts
- When adding a project that produces a NuGet package, Maven publication, container image, or other deployable artifact, add it to every applicable build and publish workflow, release bundle or manifest, artifact verifier, package-smoke consumer, and documented artifact catalog in the same change.
- Validate new packages from a staged consumer project rather than relying only on the source-project build. Keep artifact counts and release-script summaries synchronized with the published set.

## Java project
- The Java project resides in `src/Java`. See `src/Java/AGENTS.md` for instructions specific to that codebase.
