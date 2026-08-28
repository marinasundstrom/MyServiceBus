#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
runtime_async_project_dir="$repository_root/test/AotSmoke/DotNet11"
runtime_async_work_dir="$(mktemp -d)"
trap 'rm -rf "$runtime_async_work_dir"' EXIT HUP INT TERM

case "$(uname -s)-$(uname -m)" in
  Darwin-arm64) runtime_identifier="osx-arm64" ;;
  Darwin-x86_64) runtime_identifier="osx-x64" ;;
  Linux-aarch64) runtime_identifier="linux-arm64" ;;
  Linux-x86_64) runtime_identifier="linux-x64" ;;
  *)
    echo "Unsupported .NET 11 Runtime Async NativeAOT smoke platform: $(uname -s)-$(uname -m)" >&2
    exit 1
    ;;
esac

cd "$runtime_async_project_dir"

for core_project in \
  "$repository_root/src/MyServiceBus.Abstractions/MyServiceBus.Abstractions.csproj" \
  "$repository_root/src/MyServiceBus/MyServiceBus.csproj"
do
  target_framework="$(dotnet msbuild "$core_project" \
    -nologo \
    -property:EnableNet11RuntimeAsyncTarget=true \
    -getProperty:TargetFramework)"
  features="$(dotnet msbuild "$core_project" \
    -nologo \
    -property:EnableNet11RuntimeAsyncTarget=true \
    -getProperty:Features)"

  if [[ "$target_framework" != "net11.0" || ";$features;" != *";runtime-async=on;"* ]]; then
    echo "Core Runtime Async target is not configured for $core_project" >&2
    exit 1
  fi
done

dotnet publish \
  MyServiceBus.RuntimeAsyncAotSmoke.csproj \
  --configuration Release \
  --runtime "$runtime_identifier" \
  --property:EnableNet11RuntimeAsyncTarget=true \
  --output "$runtime_async_work_dir/publish"

"$runtime_async_work_dir/publish/MyServiceBus.RuntimeAsyncAotSmoke"
