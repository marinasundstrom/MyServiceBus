# CheckedExceptions Analyzer Guidelines

The C# codebase uses the [`Sundstrom.CheckedExceptions`](https://github.com/sundstrom/checked-exceptions) analyzer to approximate Java's checked exception model. This document outlines how to apply the analyzer so that C# code remains in step with the Java implementation.

## Handling vs. declaring

Like Java, any operation that can throw a checked exception must either handle it locally or declare it. In C#, handling is done with `try`/`catch` blocks. Declaring is done with the `[Throws(typeof(ExceptionType))]` attribute:

```csharp
[Throws(typeof(IOException))]
public async Task WriteAsync(string path)
{
    using var stream = File.OpenWrite(path); // may throw IOException
    await stream.WriteAsync(...);
}
```

This mirrors Java's `throws IOException` clause, helping maintain parity with the Java project.

## Propagating and documenting exceptions

- Catch exceptions that you can meaningfully handle. Let others bubble up, but annotate the method with `[Throws]` for each propagated exception type.
- Wrap low-level exceptions in domain-specific exceptions when exposing errors to callers, keeping the original exception in `InnerException` for context.

## Compatibility when overriding

When overriding members, keep `[Throws]` declarations compatible with the base member, as the analyzer enforces the same override rules that Java uses for checked exceptions. You can narrow the set of declared exceptions, but you cannot introduce new ones that the base member does not declare.

## Treating analyzer warnings

The analyzer reports diagnostics such as `THROWS001` when an exception isn't declared or handled. Treat these diagnostics as warnings and resolve them in the code—avoid automatic fixes that add clutter.

## Exception classification

The analyzer mirrors Java's checked-vs.-runtime distinction. By default, all exceptions are treated as *Strict* (checked), requiring a `try`/`catch` or a `[Throws]` declaration. Exceptions that represent programming errors or cancellation are configured as unchecked in `CheckedExceptions.settings.json` using the `Ignored` or `Informational` classifications. Examples include `ArgumentException`, `InvalidOperationException`, `NullReferenceException`, `OperationCanceledException`, `TaskCanceledException`, `NotImplementedException`, `KeyNotFoundException`, and `SecurityException`.

## Aligning with Java

The Java project in `src/Java` relies on Java's built-in checked exception system. Using `[Throws]` in C# keeps exception semantics aligned between the two codebases, making it easier to reason about cross-language behavior and port features between implementations.

## Auditing exception declarations

When synchronizing the C# and Java codebases or introducing new operations, use this workflow to verify that exceptions are declared consistently:

1. **Strip existing annotations** – temporarily remove `[Throws]` attributes so the analyzer reports every propagated exception.
2. **Build the solution** – treat `THROWS001` diagnostics as warnings that highlight missing declarations.
3. **Decide for each warning**
   - Handle the exception locally when possible.
   - Otherwise, determine the appropriate exception type by checking MassTransit APIs and comparing with the Java implementation's `throws` clauses.
4. **Reintroduce `[Throws]` attributes** – add `[Throws(typeof(ExceptionType))]` for each exception that should surface, favoring domain-specific exceptions that wrap the original as `InnerException`.
5. **Verify** – rebuild to ensure the warnings are resolved and run tests to confirm behavior.


