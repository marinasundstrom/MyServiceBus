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
- When writing XML documentation or Javadoc, list every exception the member can throw, regardless of whether it is Strict, Non-strict, or Informational.

## Designing public APIs

Cross-language features should expose a consistent exception surface area. When adding or evolving public methods:

- Prefer exceptions that exist in both .NET and Java. When neither platform has a suitable built-in type, create a domain-specific exception with the same name and semantics in each language.
- Declare the same set of checked exceptions in both languages using `[Throws]` in C# and `throws` clauses in Java.
- Treat recoverable conditions that callers are expected to handle as *Strict* exceptions. Use *Non-strict* or *Informational* classifications for programmer errors, cancellation, or other unrecoverable situations.
- Avoid leaking low-level framework exceptions. Wrap them in domain-specific exceptions so that the public API conveys stable, intent-revealing failure modes.
- Keep the public surface area focused: expose only exceptions that give callers actionable choices and document them in XML docs and Javadoc.

## Compatibility when overriding

When overriding members, keep `[Throws]` declarations compatible with the base member, as the analyzer enforces the same override rules that Java uses for checked exceptions. You can narrow the set of declared exceptions, but you cannot introduce new ones that the base member does not declare.

## Treating analyzer warnings

The analyzer reports diagnostics such as `THROWS001` when an exception isn't declared or handled. Treat these diagnostics as warnings and resolve them in the code—avoid automatic fixes that add clutter.

## Exception classification

The analyzer mirrors Java's checked-vs.-runtime distinction. By default, all exceptions are treated as *Strict* (checked), requiring a `try`/`catch` or a `[Throws]` declaration. Exceptions that represent programming errors or cancellation are configured as unchecked in `CheckedExceptions.settings.json` using the `Ignored` or `Informational` classifications. Examples include `ArgumentException`, `InvalidOperationException`, `NullReferenceException`, `OperationCanceledException`, `TaskCanceledException`, and `NotImplementedException`.

## Common .NET and Java exception mapping

| Java exception | .NET equivalent | C# classification |
| --- | --- | --- |
| `java.io.IOException` | `System.IO.IOException` | Strict |
| `java.io.FileNotFoundException` | `System.IO.FileNotFoundException` | Strict |
| `java.net.SocketException` | `System.Net.Sockets.SocketException` | Strict |
| `java.util.concurrent.TimeoutException` | `System.TimeoutException` | Strict |
| `java.io.EOFException` | `System.IO.EndOfStreamException` | Strict |
| `java.lang.IllegalArgumentException` | `System.ArgumentException` | Non-strict (Ignored) |
| `java.lang.IndexOutOfBoundsException` | `System.IndexOutOfRangeException` | Informational |
| `java.lang.IllegalStateException` | `System.InvalidOperationException` | Informational |
| `java.lang.UnsupportedOperationException` | `System.NotSupportedException` | Informational |
| `java.lang.NullPointerException` | `System.NullReferenceException` | Informational |
| `java.lang.InterruptedException` | `System.OperationCanceledException` / `System.Threading.Tasks.TaskCanceledException` | Informational |

## Aligning with Java

The Java project in `src/Java` relies on Java's built-in checked exception system. Using `[Throws]` in C# keeps exception semantics aligned between the two codebases, making it easier to reason about cross-language behavior and port features between implementations.

