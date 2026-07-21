#!/usr/bin/env sh
set -eu

package_dir="${1:-artifacts/packages}"
version="${2:-0.1.0-preview.1}"
packages="MyServiceBus.Abstractions MyServiceBus MyServiceBus.RabbitMq MyServiceBus.Testing"

for package_id in $packages; do
  package="$package_dir/$package_id.$version.nupkg"
  symbols="$package_dir/$package_id.$version.snupkg"
  test -f "$package"
  test -f "$symbols"

  nuspec="$(unzip -p "$package" '*.nuspec')"
  printf '%s' "$nuspec" | grep -Fq "<id>$package_id</id>"
  printf '%s' "$nuspec" | grep -Fq "<version>$version</version>"
  printf '%s' "$nuspec" | grep -Fq '<authors>Marina Sundström</authors>'
  printf '%s' "$nuspec" | grep -Fq '<license type="expression">MIT</license>'
  printf '%s' "$nuspec" | grep -Fq '<projectUrl>https://github.com/marinasundstrom/MyServiceBus</projectUrl>'
done

actual_packages="$(find "$package_dir" -maxdepth 1 -type f \( -name '*.nupkg' -o -name '*.snupkg' \) | wc -l | tr -d ' ')"
test "$actual_packages" = 8

echo "Verified four NuGet packages and four symbol packages for $version."
