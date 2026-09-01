#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_directory="${1:-$repository_root/artifacts/packages}"
package_version="${2:-}"

if [[ -z "$package_version" ]]; then
  package_version="$(dotnet msbuild "$repository_root/src/MyServiceBus/MyServiceBus.csproj" -nologo -getProperty:PackageVersion)"
fi

mkdir -p "$package_directory"
package_directory="$(cd "$package_directory" && pwd)"

# Run beneath a .NET 11 global.json so the two core packages can emit both
# net10.0 and experimental net11.0 assets. The other packages remain net10.0.
cd "$repository_root/test/AotSmoke/DotNet11"

dotnet pack "$repository_root/src/MyServiceBus.Abstractions/MyServiceBus.Abstractions.csproj" \
  --configuration Release \
  --output "$package_directory" \
  -p:IncludeExperimentalNet11Target=true \
  -p:PackageVersion="$package_version"

dotnet pack "$repository_root/src/MyServiceBus/MyServiceBus.csproj" \
  --configuration Release \
  --output "$package_directory" \
  -p:IncludeExperimentalNet11Target=true \
  -p:PackageVersion="$package_version"

projects=(
  MyServiceBus.Generators
  MyServiceBus.Serialization.Bson
  MyServiceBus.PostgreSql
  MyServiceBus.Inspection
  MyServiceBus.Monitoring
  MyServiceBus.RabbitMq
  MyServiceBus.AzureServiceBus
  MyServiceBus.AmazonSqs
  MyServiceBus.Testing
)

for project in "${projects[@]}"; do
  dotnet pack "$repository_root/src/$project/$project.csproj" \
    --no-build \
    --configuration Release \
    --output "$package_directory" \
    -p:PackageVersion="$package_version"
done
