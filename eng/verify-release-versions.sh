#!/usr/bin/env sh
set -eu

expected_version="${1:-}"

version_prefix="$(sed -n 's:.*<VersionPrefix>\([^<]*\)</VersionPrefix>.*:\1:p' Directory.Build.props)"
version_suffix="$(sed -n 's:.*<VersionSuffix>\([^<]*\)</VersionSuffix>.*:\1:p' Directory.Build.props)"
java_version="$(sed -n "s/^version = '\([^']*\)'/\1/p" build.gradle)"
dotnet_version="$version_prefix-$version_suffix"

test -n "$version_prefix"
test -n "$version_suffix"
test -n "$java_version"

if [ "$dotnet_version" != "$java_version" ]; then
  echo ".NET version '$dotnet_version' does not match Java version '$java_version'." >&2
  exit 1
fi

if [ -n "$expected_version" ] && [ "$java_version" != "$expected_version" ]; then
  echo "Release version '$java_version' does not match expected version '$expected_version'." >&2
  exit 1
fi

echo "Verified synchronized .NET and Java release version $java_version."
