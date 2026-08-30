#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_dir/../../.." && pwd)"
prototype_directory="$(mktemp -d "${TMPDIR:-/tmp}/myservicebus-union-prototype.XXXXXX")"
package_feed="$prototype_directory/feed"
package_cache="$prototype_directory/packages"
package_version="0.1.0-unionprototype"

cleanup() {
  rm -rf "$prototype_directory"
}
trap cleanup EXIT

mkdir -p "$package_feed" "$package_cache"
cd "$script_dir"

dotnet pack "$repository_root/src/MyServiceBus.Abstractions/MyServiceBus.Abstractions.csproj" \
  --configuration Release \
  --output "$package_feed" \
  -p:EnableNet11RuntimeAsyncTarget=true \
  -p:PackageVersion="$package_version"

dotnet pack "$repository_root/src/MyServiceBus/MyServiceBus.csproj" \
  --configuration Release \
  --output "$package_feed" \
  -p:EnableNet11RuntimeAsyncTarget=true \
  -p:PackageVersion="$package_version"

dotnet restore MyServiceBus.DotNet11UnionPackageConsumer.csproj \
  --packages "$package_cache" \
  --source "$package_feed" \
  --source https://api.nuget.org/v3/index.json

dotnet run \
  --project MyServiceBus.DotNet11UnionPackageConsumer.csproj \
  --no-restore \
  -p:RestorePackagesPath="$package_cache"

dotnet restore RavenConsumer/RavenUnionConsumer.rvnproj \
  --packages "$package_cache" \
  --source "$package_feed" \
  --source https://api.nuget.org/v3/index.json

dotnet run \
  --project RavenConsumer/RavenUnionConsumer.rvnproj \
  --no-restore \
  -p:RestorePackagesPath="$package_cache" \
  -p:WarningLevel=0
