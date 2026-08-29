# AGENTS Instructions

This directory contains the Java implementation of MyServiceBus. See `../../docs/development/design-guidelines.md` for architectural and feature parity guidelines.

## Code style
- Use standard Java conventions (UpperCamelCase for classes and interfaces, lowerCamelCase for methods and variables).
- Format code using your IDE's auto-format.

## Testing
- Run the narrowest relevant Gradle test tasks before committing. For shared infrastructure such as serialization, run the core module, the affected serializer module, and relevant local broker tests.
- Run `:myservicebus-azure-service-bus:test` only for Azure transport changes or explicitly Azure-facing behavior. Reserve the full `gradle test` pass for release validation or broad cross-cutting changes.
- Use the system `gradle` rather than the checked-in Gradle wrapper.
