# MyServiceBus Java

This folder contains the Java modules for MyServiceBus built with a Maven reactor.

## Prerequisites
- JDK 17 (Temurin/OpenJDK recommended)
- Maven 3.9+
- Docker (optional) to run RabbitMQ locally

## Build
- Build all modules:
  ```bash
  mvn -DskipTests -DenforceJdk17 install
  ```
  This compiles all reactor modules and installs them to your local Maven repo.

## Run locally
### 1) Start RabbitMQ
From the repository root, start RabbitMQ using Docker Compose:
```bash
docker compose up -d rabbitmq
```
RabbitMQ defaults: host `localhost`, port `5672`, mgmt UI `http://localhost:15672` (guest/guest).

### 2) Run the Test App
Recommended (targets the module POM and auto-builds dependencies with `-am`):

- From the module directory:
  ```bash
  cd src/Java/testapp
  RABBITMQ_HOST=localhost HTTP_PORT=5301 \
    mvn -f pom.xml -am -DenforceJdk17 -DskipTests exec:java
  ```

- From the repo root (no `cd` required):
  ```bash
  RABBITMQ_HOST=localhost HTTP_PORT=5301 \
    mvn -f src/Java/testapp/pom.xml -am -DenforceJdk17 -DskipTests exec:java
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
- `GET /request_multi` – request/response with Fault handling

Run multiple instances by changing `HTTP_PORT` (e.g., 5301 and 5302).

### Environment variables
- `RABBITMQ_HOST`: RabbitMQ host (default `localhost`)
- `HTTP_PORT`: HTTP port for testapp (default `5301`)

See also: `docs/two-service-sample.md` for running .NET and Java together.

## JDK Toolchain (17)
Builds run fine on JDK 17+ (CI uses Temurin 17). If you want Maven to enforce using a specific local JDK 17 via toolchains, enable the optional profile:

```bash
mvn -DenforceJdk17 -DskipTests install
```

Then create `~/.m2/toolchains.xml` that points to a JDK 17 installation.

Example (macOS Homebrew):
```xml
<toolchains>
  <toolchain>
    <type>jdk</type>
    <provides>
      <version>17</version>
      <vendor>any</vendor>
    </provides>
    <configuration>
      <jdkHome>/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home</jdkHome>
    </configuration>
  </toolchain>
  <!-- Linux example:
  <toolchain>
    <type>jdk</type>
    <provides>
      <version>17</version>
      <vendor>any</vendor>
    </provides>
    <configuration>
      <jdkHome>/usr/lib/jvm/java-17-temurin</jdkHome>
    </configuration>
  </toolchain>
  -->
  <!-- Windows example:
  <toolchain>
    <type>jdk</type>
    <provides>
      <version>17</version>
      <vendor>any</vendor>
    </provides>
    <configuration>
      <jdkHome>C:\\Program Files\\Eclipse Adoptium\\jdk-17.0.12.7-hotspot</jdkHome>
    </configuration>
  </toolchain>
  -->
</toolchains>
```

Verify toolchain is picked up:
```bash
mvn -DskipTests -X validate | grep -i "Using toolchain: JDK"
```

## Notes
- Lombok is configured as an annotation processor via the parent POM and does not need to be added per-module.
- Reactor modules and external dependency versions are centralized in the parent `pom.xml`.
