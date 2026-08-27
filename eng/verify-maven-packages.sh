#!/usr/bin/env sh
set -eu

version="${1:-0.1.0-preview.4}"
modules="myservicebus-abstractions myservicebus-di myservicebus-logging myservicebus-tasks myservicebus myservicebus-inspection myservicebus-monitoring myservicebus-rabbitmq myservicebus-azure-service-bus myservicebus-testing"
require_signatures="${REQUIRE_MAVEN_SIGNATURES:-0}"

for artifact_id in $modules; do
  artifact_dir="src/Java/$artifact_id/build/repository/io/github/marinasundstrom/myservicebus/$artifact_id/$version"
  base="$artifact_dir/$artifact_id-$version"

  test -f "$base.jar"
  test -f "$base-sources.jar"
  test -f "$base-javadoc.jar"
  test -f "$base.module"
  test -f "$base.pom"

  grep -Fq '<groupId>io.github.marinasundstrom.myservicebus</groupId>' "$base.pom"
  grep -Fq "<artifactId>$artifact_id</artifactId>" "$base.pom"
  grep -Fq "<version>$version</version>" "$base.pom"
  grep -Fq '<name>MIT License</name>' "$base.pom"
  grep -Fq '<url>https://github.com/marinasundstrom/MyServiceBus</url>' "$base.pom"

  if [ "$require_signatures" = "1" ]; then
    for artifact in "$base.jar" "$base-sources.jar" "$base-javadoc.jar" "$base.module" "$base.pom"; do
      test -s "$artifact.asc"
    done
  else
    actual_artifacts="$(find "$artifact_dir" -maxdepth 1 -type f | wc -l | tr -d ' ')"
    test "$actual_artifacts" = 25
  fi
done

if [ "$require_signatures" = "1" ]; then
  echo "Verified ten signed Maven publications with binary, source, Javadoc, module, and POM artifacts for $version."
else
  echo "Verified ten Maven publications with binary, source, Javadoc, module, and POM artifacts for $version."
fi
