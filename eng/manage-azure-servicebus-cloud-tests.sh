#!/bin/sh
set -eu

action="${1:-status}"
resource_group="${AZURE_SERVICEBUS_RESOURCE_GROUP:-rg-myservicebus-tests}"
namespace_name="${AZURE_SERVICEBUS_NAMESPACE:-sb-myservicebus-tests-se}"
authorization_rule="${AZURE_SERVICEBUS_AUTHORIZATION_RULE:-MyServiceBusTests}"
location="${AZURE_SERVICEBUS_LOCATION:-swedencentral}"

command -v az >/dev/null 2>&1 || {
  echo "Azure CLI is required." >&2
  exit 1
}

case "$action" in
  provision)
    az group create \
      --name "$resource_group" \
      --location "$location" \
      --tags project=MyServiceBus purpose=integration-testing \
      --output none

    if ! az servicebus namespace show \
      --resource-group "$resource_group" \
      --name "$namespace_name" \
      --output none >/dev/null 2>&1; then
      available="$(az servicebus namespace exists \
        --name "$namespace_name" \
        --query nameAvailable \
        --output tsv)"
      if [ "$available" != "true" ]; then
        echo "Azure Service Bus namespace '$namespace_name' is not available." >&2
        exit 1
      fi

      az servicebus namespace create \
        --resource-group "$resource_group" \
        --name "$namespace_name" \
        --location "$location" \
        --sku Standard \
        --tags project=MyServiceBus purpose=integration-testing \
        --output none
    fi

    az servicebus namespace authorization-rule create \
      --resource-group "$resource_group" \
      --namespace-name "$namespace_name" \
      --name "$authorization_rule" \
      --rights Manage Send Listen \
      --output none
    echo "Azure Service Bus test namespace '$namespace_name' is ready."
    ;;
  teardown)
    if az servicebus namespace show \
      --resource-group "$resource_group" \
      --name "$namespace_name" \
      --output none >/dev/null 2>&1; then
      az servicebus namespace delete \
        --resource-group "$resource_group" \
        --name "$namespace_name" \
        --output none
      az servicebus namespace wait \
        --resource-group "$resource_group" \
        --name "$namespace_name" \
        --deleted \
        --interval 10 \
        --timeout 600
      echo "Azure Service Bus test namespace '$namespace_name' was deleted."
    else
      echo "Azure Service Bus test namespace '$namespace_name' does not exist."
    fi
    ;;
  status)
    az servicebus namespace show \
      --resource-group "$resource_group" \
      --name "$namespace_name" \
      --query '{name:name,location:location,sku:sku.name,status:status,provisioningState:provisioningState}' \
      --output table
    ;;
  *)
    echo "Usage: $0 provision|status|teardown" >&2
    exit 2
    ;;
esac
