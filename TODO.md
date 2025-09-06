# TODO

## Silent handling of unknown message types

Add explicit diagnostics when a message type is not registered: log a warning, emit a metric, or forward the raw payload to a dedicated “dead‑letter/unknown message” queue so the event isn’t silently discarded.

Consider enforcing explicit registration by throwing or surfacing an error when an unregistered type is encountered in non‑production environments, helping catch configuration mistakes early.

## RedeliveryCount header parsing can throw a FormatException

Replace direct Convert.ToInt32 calls with int.TryParse or a similar guard. When parsing fails, default the redelivery count to zero (or a configuration‑defined value) and log the malformed header for visibility.

Ensure malformed messages are still routed to the error queue: catch any FormatException, log it, and re‑enqueue the message with a sanitized RedeliveryCount so error handling and retry logic remain intact.

## Gradle wrapper JAR stored as a Git‑LFS pointer

Retrieve the actual wrapper JAR via git lfs fetch && git lfs checkout, or commit the wrapper JAR as a regular file so build environments without LFS support can still run the tests.

Document the need to install Git LFS (and run git lfs install) for developers who clone the repo.

## Java toolchain fails due to missing trust store

Ensure ca-certificates-java is installed and the JVM’s trust store is generated (update-ca-certificates or dpkg-reconfigure ca-certificates-java on Debian/Ubuntu).

In CI, preinstall OpenJDK and run any required trust-store initialization as part of the build image, so Gradle can download dependencies without SSL errors.