# Two-service sample

This sample runs the .NET and Java test apps together using MyServiceBus. Both apps expose the same demo endpoints and use the same message patterns so you can compare logs, faults, and traces directly.

## 1. Start RabbitMQ

Use the provided compose file to start RabbitMQ:

```bash
docker compose up rabbitmq
```

## 2. Run the .NET service

```bash
dotnet run --project src/TestApp
```

The service listens on `http://localhost:5112`.

## 3. Run the Java service

From the repository root:

```bash
./gradlew :testapp:run
```

Notes:
- `RABBITMQ_HOST` defaults to `localhost` if not set.
- `HTTP_PORT` defaults to `5301` if not set.
- See `src/Java/README.md` for full Java build/run details and optional JDK 17 toolchain enforcement.

The Java service listens on `http://localhost:5301`.

## 4. Available endpoints

Both services expose the same endpoints:

- `/publish`
- `/publish/fault`
- `/send`
- `/send/fault`
- `/request`
- `/request/fault`
- `/request_multi`
- `/request_multi/fault`

The `.http` files for invoking them are:

- `src/TestApp/TestApp.http`
- `src/Java/testapp/testapp.http`

## 5. Fault behavior

Failure cases are explicit and isolated to the `*/fault` routes. There is no randomized failure logic in the sample anymore.

- `/publish` and `/send` produce a normal `SubmitOrder`
- `/publish/fault` and `/send/fault` produce a `SubmitOrder` that the consumer intentionally faults
- `/request` and `/request_multi` produce a normal `TestRequest`
- `/request/fault` and `/request_multi/fault` produce a `TestRequest` that the consumer intentionally faults

When a `SubmitOrder` consumer faults, MyServiceBus publishes a `Fault<SubmitOrder>` to the `submit-order_fault` queue. Both sample apps register `SubmitOrderFaultConsumer` on that queue so you can compare the behavior in each runtime.

## 6. Try the flow

Send a normal publish from .NET to Java:

```bash
curl http://localhost:5112/publish
```

Send an intentional fault from Java:

```bash
curl http://localhost:5301/publish/fault
```

Test request/response from either app:

```bash
curl http://localhost:5112/request
curl http://localhost:5301/request_multi/fault
```

You should see matching trace shapes in Aspire and comparable logs in both services, with differences limited to the runtime-specific exception formatting.

## Troubleshooting
- If Gradle reports missing modules, run a full build first:
  ```bash
  (cd src/Java && ./gradlew build -x test)
  ```
- Ensure RabbitMQ is reachable at `RABBITMQ_HOST` and credentials are `guest/guest` (default in compose).
