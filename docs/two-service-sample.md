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

In separate terminals run:

```bash
HTTP_PORT=5301 mvn -f src/Java/pom.xml -pl testapp -am exec:java
HTTP_PORT=5302 mvn -f src/Java/pom.xml -pl testapp -am exec:java
```

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
