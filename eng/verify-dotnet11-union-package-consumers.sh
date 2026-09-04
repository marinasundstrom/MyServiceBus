#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_directory="${1:-$repository_root/artifacts/packages}"
package_version="${2:-0.1.0-preview.10}"
consumer_directory="$repository_root/test/Experiments/DotNet11Unions"
package_cache="$(mktemp -d "${TMPDIR:-/tmp}/myservicebus-net11-consumer.XXXXXX")"

cleanup() {
  rm -rf "$package_cache"
}
trap cleanup EXIT

package_directory="$(cd "$package_directory" && pwd)"
cd "$consumer_directory"

dotnet restore MyServiceBus.DotNet11UnionPackageConsumer.csproj \
  --force \
  --packages "$package_cache" \
  --source "$package_directory" \
  --source https://api.nuget.org/v3/index.json \
  -p:MyServiceBusPackageVersion="$package_version"

dotnet run \
  --project MyServiceBus.DotNet11UnionPackageConsumer.csproj \
  --no-restore \
  -p:RestorePackagesPath="$package_cache" \
  -p:MyServiceBusPackageVersion="$package_version"

dotnet restore RavenConsumer/RavenUnionConsumer.rvnproj \
  --force \
  --packages "$package_cache" \
  --source "$package_directory" \
  --source https://api.nuget.org/v3/index.json \
  -p:MyServiceBusPackageVersion="$package_version"

dotnet run \
  --project RavenConsumer/RavenUnionConsumer.rvnproj \
  --no-restore \
  -p:RestorePackagesPath="$package_cache" \
  -p:MyServiceBusPackageVersion="$package_version" \
  -p:WarningLevel=0
