#!/usr/bin/env sh
set -eu

assert_contains() {
  file="$1"
  value="$2"
  grep -Fq "$value" "$file"
}

assert_contains global.json '"version": "10.0.100"'
assert_contains .github/workflows/dotnet.yml "dotnet-version: '10.0.100'"
assert_contains .github/workflows/interop.yml "dotnet-version: '10.0.100'"
assert_contains .github/workflows/java.yml "java-version: '17'"
assert_contains .github/workflows/java.yml "gradle-version: '9.0.0'"
assert_contains build.gradle 'languageVersion = JavaLanguageVersion.of(17)'
assert_contains build.gradle "rabbitmqVersion = '5.20.0'"
assert_contains Directory.Packages.props '<PackageVersion Include="RabbitMQ.Client" Version="7.2.1" />'
assert_contains Directory.Packages.props '<PackageVersion Include="MassTransit.RabbitMQ" Version="8.5.1" />'
assert_contains test/MyServiceBus.RabbitMq.Tests/RabbitMqTestcontainerTests.cs 'rabbitmq:4.1.8-alpine'
assert_contains src/Java/myservicebus-rabbitmq/src/test/java/com/myservicebus/rabbitmq/RabbitMqTestcontainerTest.java 'rabbitmq:4.1.8-alpine'
assert_contains docs/supported-versions.md '.NET SDK `10.0.100`'
assert_contains docs/supported-versions.md '`rabbitmq:4.1.8-alpine`'
assert_contains docs/supported-versions.md '`MassTransit.RabbitMQ` `8.5.1`'
assert_contains docs/supported-versions.md '`com.rabbitmq:amqp-client` `5.20.0`'

if rg -q 'rabbitmq:4\.1-alpine' test src/Java; then
  echo 'Found an unpinned RabbitMQ 4.1 Testcontainers image.' >&2
  exit 1
fi

echo 'Verified the documented MVP runtime and interoperability baselines.'
