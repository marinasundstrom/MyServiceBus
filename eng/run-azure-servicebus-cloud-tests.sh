#!/bin/sh
set -eu

resource_group="${AZURE_SERVICEBUS_RESOURCE_GROUP:-rg-myservicebus-tests}"
namespace_name="${AZURE_SERVICEBUS_NAMESPACE:-sb-myservicebus-tests-se}"
authorization_rule="${AZURE_SERVICEBUS_AUTHORIZATION_RULE:-MyServiceBusTests}"

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
  --filter FullyQualifiedName~AzureServiceBusCloudAcceptanceTests
gradle :myservicebus-azure-service-bus:test --rerun-tasks \
  --tests com.myservicebus.azure.servicebus.AzureServiceBusCloudTest
