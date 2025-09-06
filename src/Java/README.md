# MyServiceBus Java

This folder contains the Java modules for MyServiceBus built with a Maven reactor.

## Build
- Build all modules:
  ```bash
  mvn -DskipTests install
  ```
- Build and run only the test app from the repo root:
  ```bash
  RABBITMQ_HOST=localhost HTTP_PORT=5301 \
    mvn -f src/Java/pom.xml -pl testapp -am -DskipTests exec:java
  ```

## JDK Toolchain (17)
Maven enforces JDK 17 using the Toolchains plugin. Create `~/.m2/toolchains.xml` that points to a JDK 17 installation.

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

