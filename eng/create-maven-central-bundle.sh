#!/usr/bin/env sh
set -eu

output="${1:-build/maven-central-bundle.zip}"
version="${2:-0.1.0-preview.6}"
modules="myservicebus-abstractions myservicebus-di myservicebus-logging myservicebus-tasks myservicebus myservicebus-processor myservicebus-serialization-bson myservicebus-postgresql myservicebus-inspection myservicebus-monitoring myservicebus-rabbitmq myservicebus-azure-service-bus myservicebus-testing"
staging_dir="$(mktemp -d)"

case "$version" in
  *[!A-Za-z0-9._-]*)
    echo "Invalid Maven version '$version'." >&2
    exit 1
    ;;
esac

cleanup() {
  rm -rf "$staging_dir"
}
trap cleanup EXIT HUP INT TERM

output_dir="$(dirname "$output")"
output_name="$(basename "$output")"
mkdir -p "$output_dir"
output_dir="$(cd "$output_dir" && pwd)"

for module in $modules; do
  repository="src/Java/$module/build/repository"
  version_directory="$repository/io/github/marinasundstrom/myservicebus/$module/$version"
  test -d "$version_directory"
  mkdir -p "$staging_dir/io/github/marinasundstrom/myservicebus/$module"
  cp -R "$version_directory" "$staging_dir/io/github/marinasundstrom/myservicebus/$module/"
done

bundle="$staging_dir/$output_name"
(cd "$staging_dir" && zip -qr "$bundle" io)
mv "$bundle" "$output_dir/$output_name"
test -s "$output_dir/$output_name"

echo "Created Maven Central deployment bundle at $output_dir/$output_name."
