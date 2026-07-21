#!/usr/bin/env sh
set -eu

repository_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"

dotnet restore "$repository_root/test/PackageSmoke/DotNet/PackageSmoke.csproj" \
  --source "$repository_root/artifacts/packages" \
  --source https://api.nuget.org/v3/index.json
dotnet run --project "$repository_root/test/PackageSmoke/DotNet/PackageSmoke.csproj" \
  --configuration Release \
  --no-restore
