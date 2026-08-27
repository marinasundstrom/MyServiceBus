#!/bin/sh
set -eu

script_directory="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
resource_group="${AZURE_SERVICEBUS_RESOURCE_GROUP:-rg-myservicebus-tests}"
namespace_name="${AZURE_SERVICEBUS_NAMESPACE:-sb-myservicebus-tests-se}"
authorization_rule="${AZURE_SERVICEBUS_AUTHORIZATION_RULE:-MyServiceBusTests}"
ephemeral=false

case "${1:-}" in
  "")
    ;;
  --ephemeral)
    ephemeral=true
    namespace_name="${AZURE_SERVICEBUS_EPHEMERAL_NAMESPACE:-sb-msb-tests-$(date -u +%Y%m%d%H%M%S)-$$}"
    export AZURE_SERVICEBUS_NAMESPACE="$namespace_name"
    "$script_directory/manage-azure-servicebus-cloud-tests.sh" provision
    ;;
  *)
    echo "Usage: $0 [--ephemeral]" >&2
    exit 2
    ;;
esac

cleanup() {
  exit_status=$?
  trap - EXIT HUP INT TERM
  if [ "$ephemeral" = true ]; then
    if ! "$script_directory/manage-azure-servicebus-cloud-tests.sh" teardown; then
      if [ "$exit_status" -eq 0 ]; then
        exit_status=1
      fi
    fi
  fi
  exit "$exit_status"
}

trap cleanup EXIT HUP INT TERM

command -v az >/dev/null 2>&1 || {
  echo "Azure CLI is required." >&2
  exit 1
}

connection_string="$(az servicebus namespace authorization-rule keys list \
  --resource-group "$resource_group" \
  --namespace-name "$namespace_name" \
  --name "$authorization_rule" \
  --query primaryConnectionString \
  --output tsv)"

if [ -z "$connection_string" ]; then
  echo "Azure Service Bus did not return a connection string." >&2
  exit 1
fi

export RUN_AZURE_SERVICEBUS_CLOUD_TESTS=1
export AZURE_SERVICEBUS_CLOUD_CONNECTION_STRING="$connection_string"

dotnet test test/MyServiceBus.AzureServiceBus.Tests/MyServiceBus.AzureServiceBus.Tests.csproj \
  --filter "FullyQualifiedName~AzureServiceBusCloudAcceptanceTests|FullyQualifiedName~MassTransitAzureServiceBusInteropTests"
gradle :myservicebus-azure-service-bus:test --rerun-tasks \
  --tests com.myservicebus.azure.servicebus.AzureServiceBusCloudTest
