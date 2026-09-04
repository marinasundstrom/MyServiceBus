#!/usr/bin/env sh
set -eu

package_dir="${1:-artifacts/packages}"
version="${2:-0.1.0-preview.10}"
packages="Sundstrom.MyServiceBus.Abstractions Sundstrom.MyServiceBus Sundstrom.MyServiceBus.Generators Sundstrom.MyServiceBus.Serialization.Bson Sundstrom.MyServiceBus.PostgreSql Sundstrom.MyServiceBus.Inspection Sundstrom.MyServiceBus.Monitoring Sundstrom.MyServiceBus.RabbitMq Sundstrom.MyServiceBus.AzureServiceBus Sundstrom.MyServiceBus.AmazonSqs Sundstrom.MyServiceBus.Testing"

for package_id in $packages; do
  package="$package_dir/$package_id.$version.nupkg"
  test -f "$package"

  if [ "$package_id" != "Sundstrom.MyServiceBus.Generators" ]; then
    symbols="$package_dir/$package_id.$version.snupkg"
    test -f "$symbols"
  fi

  nuspec="$(unzip -p "$package" '*.nuspec')"
  printf '%s' "$nuspec" | grep -Fq "<id>$package_id</id>"
  printf '%s' "$nuspec" | grep -Fq "<version>$version</version>"
  printf '%s' "$nuspec" | grep -Fq '<authors>Marina Sundström</authors>'
  printf '%s' "$nuspec" | grep -Fq '<license type="expression">MIT</license>'
  printf '%s' "$nuspec" | grep -Fq '<projectUrl>https://github.com/marinasundstrom/MyServiceBus</projectUrl>'
done

generator_contents="$(unzip -Z1 "$package_dir/Sundstrom.MyServiceBus.Generators.$version.nupkg")"
printf '%s\n' "$generator_contents" | grep -Fq 'analyzers/dotnet/cs/MyServiceBus.Generators.dll'

for package_id in Sundstrom.MyServiceBus.Abstractions Sundstrom.MyServiceBus; do
  package="$package_dir/$package_id.$version.nupkg"
  contents="$(unzip -Z1 "$package")"
  case "$package_id" in
    Sundstrom.MyServiceBus.Abstractions) assembly="MyServiceBus.Abstractions.dll" ;;
    Sundstrom.MyServiceBus) assembly="MyServiceBus.dll" ;;
  esac
  printf '%s\n' "$contents" | grep -Fq "lib/net10.0/$assembly"
  printf '%s\n' "$contents" | grep -Fq "lib/net11.0/$assembly"
done

actual_packages="$(find "$package_dir" -maxdepth 1 -type f \
  \( -name "*.$version.nupkg" -o -name "*.$version.snupkg" \) | wc -l | tr -d ' ')"
test "$actual_packages" = 21

echo "Verified eleven NuGet packages and ten symbol packages for $version, including the analyzer-only generator and experimental .NET 11 core assets."
