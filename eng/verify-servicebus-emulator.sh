#!/usr/bin/env sh
set -eu

fixture_dir="test/AzureServiceBusEmulator"
compose_file="$fixture_dir/compose.yaml"
config_file="$fixture_dir/config.json"

jq -e '
  .UserConfig.Namespaces | length == 1 and
  .[0].Name == "sbemulatorns" and
  (.[0].Queues | map(.Name) | contains([
    "msb-direct",
    "msb-publish",
    "msb-publish_error",
    "msb-publish_skipped",
    "msb-publish-fault-observer",
    "msb-response"
  ])) and
  (.[0].Topics | map(.Name) | contains([
    "msb-compatibility-message",
    "msb-publish_fault"
  ]))
' "$config_file" >/dev/null

docker compose -f "$compose_file" config --quiet

if rg -q 'servicebus-emulator:(latest|2\.0\.0)([[:space:]]|$)' "$fixture_dir"; then
  echo 'Found an unapproved Azure Service Bus emulator image tag.' >&2
  exit 1
fi

echo 'Verified the Azure Service Bus emulator fixture.'
