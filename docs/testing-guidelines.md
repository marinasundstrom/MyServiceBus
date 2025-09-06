# Testing Guidelines

These guidelines describe how to structure and run tests for MyServiceBus across languages.

## Automated tests

### Unit tests

- **C#**: place tests in a separate `*.Tests` project. Use the `InternalsVisibleTo` attribute in the production project to expose internal members to its test project when needed.
- **Java**: keep tests alongside the source within the same module, typically under `src/test/java`. Java does not require a separate project or visibility attributes; package-private members can be accessed directly.

Unit tests should verify small pieces of behavior and run quickly. When a feature is added or changed, add corresponding unit tests in both language implementations.

For guidance on using the in-memory harness when writing tests, see [testing.md](testing.md).
