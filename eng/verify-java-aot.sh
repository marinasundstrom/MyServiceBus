#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
java_aot_work_dir="$(mktemp -d)"
trap 'rm -rf "$java_aot_work_dir"' EXIT HUP INT TERM

cd "$repository_root"
gradle :java-aot-smoke:jar --no-daemon

smoke_jar="$(find "$repository_root/src/Java/java-aot-smoke/build/libs" \
  -maxdepth 1 -name 'java-aot-smoke-*.jar' -print -quit)"
test -n "$smoke_jar"
graal_image="ghcr.io/graalvm/native-image-community:21"

docker run --rm \
  --volume "$smoke_jar:/workspace/app.jar:ro" \
  --volume "$java_aot_work_dir:/output" \
  "$graal_image" \
  --no-fallback \
  -jar /workspace/app.jar \
  -o /output/myservicebus-java-aot-smoke

docker run --rm \
  --entrypoint /workspace/myservicebus-java-aot-smoke \
  --volume "$java_aot_work_dir/myservicebus-java-aot-smoke:/workspace/myservicebus-java-aot-smoke:ro" \
  "$graal_image"
