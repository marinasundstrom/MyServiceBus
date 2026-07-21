#!/usr/bin/env sh
set -eu

version="${1:-0.1.0-preview.1}"
modules="myservicebus-abstractions myservicebus-di myservicebus-logging myservicebus-tasks myservicebus myservicebus-rabbitmq myservicebus-testing"

for artifact_id in $modules; do
  artifact_dir="src/Java/$artifact_id/build/repository/com/myservicebus/$artifact_id/$version"
  base="$artifact_dir/$artifact_id-$version"

  test -f "$base.jar"
  test -f "$base-sources.jar"
  test -f "$base-javadoc.jar"
  test -f "$base.module"
  test -f "$base.pom"

  grep -Fq '<groupId>com.myservicebus</groupId>' "$base.pom"
  grep -Fq "<artifactId>$artifact_id</artifactId>" "$base.pom"
  grep -Fq "<version>$version</version>" "$base.pom"
  grep -Fq '<name>MIT License</name>' "$base.pom"
  grep -Fq '<url>https://github.com/marinasundstrom/MyServiceBus</url>' "$base.pom"

  actual_artifacts="$(find "$artifact_dir" -maxdepth 1 -type f | wc -l | tr -d ' ')"
  test "$actual_artifacts" = 25
done

echo "Verified seven Maven publications with binary, source, Javadoc, module, and POM artifacts for $version."
