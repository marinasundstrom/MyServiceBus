# AGENTS Instructions

This repository contains a .NET solution for MyServiceBus and a Java project. Follow these guidelines when contributing:

## Code style
- Use standard C# conventions (PascalCase for types and methods, camelCase for locals and parameters).
- Prefer `dotnet format` to automatically format files.
 
## Exception handling
- The CheckedExceptions analyzer is enabled; treat THROWS diagnostics as warnings and avoid auto-fixes that clutter code.
- Handle exceptions (indicated by diagnostic THROWS001) locally by catching them. If an exception flows out of a method, declare it with `[Throws(typeof(ExceptionType))]`.
- When overriding members, ensure your `Throws` declarations remain compatible with the base member.
- Declare and use domain-specific exceptions when no built-in type clearly conveys the problem, wrapping the original exception as the `InnerException` for context. Domain-specific exceptions are preferred when surfacing errors to API consumers in a meaningful way.

## Testing
- From the repository root, run `dotnet test` and ensure all tests pass before committing.

## Documentation
- Write documentation in Markdown and place files in the `docs/` folder when appropriate.
- See `docs/design-goals.md` for overarching design goals, including MassTransit familiarity and C#↔Java parity.

## Java project
- The Java project resides in `src/Java`. See `src/Java/AGENTS.md` for instructions specific to that codebase.

