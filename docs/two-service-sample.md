# Two-service sample

This sample runs the existing .NET and Java test apps together using MyServiceBus. The Java service is run twice to demonstrate multiple consumers processing the same queue.

## 1. Start RabbitMQ

Use the provided compose file to start RabbitMQ:

```bash
docker compose up rabbitmq
```

## 2. Run the .NET service

```bash
dotnet run --project src/TestApp
```

The service listens on `http://localhost:5112` and publishes `SubmitOrder` messages when its `/publish` endpoint is called.

## 3. Run two Java service replicas

Run the Java test app twice on different ports. The Maven reactor will build only what’s needed (`-pl testapp -am`).

In two separate terminals, from the repository root:

```bash
# Build all Java modules (once)
mvn -f src/Java/pom.xml -DskipTests install 

# Instance 1 (module POM; builds needed deps)
RABBITMQ_HOST=localhost HTTP_PORT=5301 \
  mvn -f src/Java/testapp/pom.xml -am -DskipTests exec:java

# Instance 2 (module POM; builds needed deps)
RABBITMQ_HOST=localhost HTTP_PORT=5302 \
  mvn -f src/Java/testapp/pom.xml -am -DskipTests exec:java
```

Notes:
- `RABBITMQ_HOST` defaults to `localhost` if not set.
- `HTTP_PORT` selects the HTTP port for each replica.
- `-pl testapp -am` builds `testapp` and its dependencies only.
- See `src/Java/README.md` for full Java build/run details and optional JDK 17 toolchain enforcement.

Each instance consumes `SubmitOrder` messages from the same queue.

## 4. Publish a message

From another terminal call the .NET service:

```bash
curl http://localhost:5112/publish
```

One of the Java replicas logs the `SubmitOrder` and responds with `OrderSubmitted`, including its `HTTP_PORT` so the .NET service logs which replica processed the order.

To send from Java instead, call either Java instance:

```bash
curl http://localhost:5301/publish
```

The .NET service logs the received `SubmitOrder`.

## Troubleshooting
- If Maven reports missing modules, run a full reactor build first:
  ```bash
  (cd src/Java && mvn -DskipTests install)
  ```
- Ensure RabbitMQ is reachable at `RABBITMQ_HOST` and credentials are `guest/guest` (default in compose).
