#!/usr/bin/env sh
set -eu

repository_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"

gradle \
  --project-dir "$repository_root/test/PackageSmoke/Java" \
  --no-daemon \
  --refresh-dependencies \
  -PstagingRepositoryRoot="$repository_root/src/Java" \
  run
