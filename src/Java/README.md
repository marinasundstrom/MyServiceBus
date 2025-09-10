# MyServiceBus Java

This folder contains the Java modules for MyServiceBus. The Gradle multi-project build resides at the repository root.

## Prerequisites
- JDK 17 (Temurin/OpenJDK recommended)
- Docker (optional) to run RabbitMQ locally

## Build
If the Gradle wrapper JAR is missing, bootstrap it from the repository root:
```bash
gradle wrapper
```

- From the repository root, build all modules and run tests:
  ```bash
  ./gradlew test
  ```

## Run locally
### 1) Start RabbitMQ
From the repository root, start RabbitMQ using Docker Compose:
```bash
docker compose up -d rabbitmq
```
RabbitMQ defaults: host `localhost`, port `5672`, mgmt UI `http://localhost:15672` (guest/guest).

### 2) Run the Test App
- From the module directory:
  ```bash
  cd src/Java/testapp
  RABBITMQ_HOST=localhost HTTP_PORT=5301 ../../gradlew run
  ```
- From the repo root (no `cd` required):
  ```bash
  RABBITMQ_HOST=localhost HTTP_PORT=5301 ./gradlew -p src/Java/testapp run
  ```

Helper script (equivalent):
```bash
cd src/Java/testapp
RABBITMQ_HOST=localhost HTTP_PORT=5301 ./run.sh
```

The app starts an HTTP server (default port 5301) with routes:
- `GET /publish` – publishes SubmitOrder
- `GET /send` – sends SubmitOrder to a queue
- `GET /request` – request/response demo
- `GET /request_multi` – request/response with Fault handling`

Run multiple instances by changing `HTTP_PORT` (e.g., 5301 and 5302).

### Environment variables
- `RABBITMQ_HOST`: RabbitMQ host (default `localhost`)
- `HTTP_PORT`: HTTP port for testapp (default `5301`)

See also: `docs/two-service-sample.md` for running .NET and Java together.

## Notes
- Lombok is configured as an annotation processor via the root `build.gradle` and does not need to be added per-module.
- Modules and external dependency versions are centralized in the root `build.gradle`.
