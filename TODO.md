# TODO

## Silent handling of unknown message types

Add explicit diagnostics when a message type is not registered: log a warning, emit a metric, or forward the raw payload to a dedicated “dead‑letter/unknown message” queue so the event isn’t silently discarded.

Consider enforcing explicit registration by throwing or surfacing an error when an unregistered type is encountered in non‑production environments, helping catch configuration mistakes early.