# Connection Recovery

The RabbitMQ transports in both the C# and Java implementations now handle
lost connections transparently.

- A shared `ConnectionProvider` caches the active connection and verifies it
  is still open before each use.
- If the connection drops, the provider attempts to create a new one using an
  exponential backoff strategy and resets the cached instance when RabbitMQ
  signals a shutdown.
- `ConnectionFactory` enables `AutomaticRecoveryEnabled` and
  `TopologyRecoveryEnabled` so the RabbitMQ client re-establishes TCP links and
  redeclares exchanges and queues.

These safeguards allow application code to await a usable connection without
having to implement custom retry logic.

