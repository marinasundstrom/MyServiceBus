#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_aot_work_dir="$(mktemp -d)"
trap 'rm -rf "$dotnet_aot_work_dir"' EXIT HUP INT TERM

case "$(uname -s)-$(uname -m)" in
  Darwin-arm64) runtime_identifier="osx-arm64" ;;
  Darwin-x86_64) runtime_identifier="osx-x64" ;;
  Linux-aarch64) runtime_identifier="linux-arm64" ;;
  Linux-x86_64) runtime_identifier="linux-x64" ;;
  *)
    echo "Unsupported .NET NativeAOT smoke platform: $(uname -s)-$(uname -m)" >&2
    exit 1
    ;;
esac

dotnet publish \
  "$repository_root/test/AotSmoke/DotNet/MyServiceBus.AotSmoke.csproj" \
  --configuration Release \
  --runtime "$runtime_identifier" \
  --output "$dotnet_aot_work_dir/publish"

"$dotnet_aot_work_dir/publish/MyServiceBus.AotSmoke"
