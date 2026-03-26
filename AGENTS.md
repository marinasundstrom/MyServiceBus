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
- From the repository root, run `dotnet test` and ensure all tests pass before committing.
- If your changes only affect documentation (e.g., Markdown files or other non-code assets), you may skip running build or test steps.
- When adding features or changing API/behavior, implement them for both the C# and Java codebases.
- Create or update tests for each language to cover new functionality.

## Documentation
- Write documentation in Markdown and place files in the `docs/` folder when appropriate.
- See `docs/development/design-goals.md` for overarching design goals, including MassTransit familiarity and C#↔Java parity.
- Review `docs/specs/myservicebus-spec.md`, `docs/development/design-guidelines.md`, and related design documents for solution details; keep them and other docs up to date.
- `docs/feature-walkthrough.md` is the canonical source for usage samples of MyServiceBus.
- Keep `CHANGELOG.md` up to date for significant repository changes. Prefer chronological entries that summarize the larger themes of a change set rather than exhaustive commit-by-commit notes.

## Java project
- The Java project resides in `src/Java`. See `src/Java/AGENTS.md` for instructions specific to that codebase.
