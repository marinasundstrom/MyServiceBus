# Azure Service Bus Emulator Fixture

This fixture supplies the pre-provisioned topology for the proposed Azure
Service Bus transport's local data-plane and interoperability tests. It is test
infrastructure, not evidence that the transport is implemented or supported.

The fixture pins:

- Azure Service Bus emulator `2.0.1`
- SQL Server 2022 CU26 on Ubuntu 22.04
- AMQP data-plane port `5672`
- management and health port `5300`

The emulator SDK connection string is:

```text
Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

Start the fixture from the repository root:

```bash
docker compose -f test/AzureServiceBusEmulator/compose.yaml up -d
curl --fail --retry 30 --retry-delay 2 --retry-all-errors \
  http://localhost:5300/health
```

Stop it and remove its anonymous state:

```bash
docker compose -f test/AzureServiceBusEmulator/compose.yaml down --volumes
```

Set `MSB_SERVICEBUS_SQL_PASSWORD` to override the local fixture password. Do not
use that development password outside this disposable Compose project.

## Test topology

| Entity | Kind | Purpose |
| --- | --- | --- |
| `msb-direct` | Queue | Directed-send scenarios |
| `msb-compatibility-message` | Topic | Publish scenarios |
| `msb-publish` | Queue | Receive endpoint for published messages |
| `msb-publish` | Subscription | Forwards the compatibility topic to the endpoint queue |
| `msb-publish_error` | Queue | Exhausted consumer failures |
| `msb-publish_skipped` | Queue | Unrecognized message types |
| `msb-publish_fault` | Topic | Endpoint-specific `Fault<T>` publication |
| `msb-publish-fault-observer` | Queue/subscription | Observes endpoint fault publications |
| `msb-response` | Queue | Sequential emulator request/response scenarios |

The emulator uses fixed port `5672`, static entity names, and non-persistent
state. Tests sharing this fixture must run sequentially. Restart the Compose
project when a scenario requires empty entities.

The official emulator management endpoint is currently supported natively only
by the .NET administration client. Both language suites therefore consume the
same declarative `config.json` and use the transport's planned
`PreProvisioned` topology mode.

See Microsoft's [local testing guide](https://learn.microsoft.com/azure/service-bus-messaging/test-locally-with-service-bus-emulator)
for emulator connection and management details and the [emulator overview](https://learn.microsoft.com/azure/service-bus-messaging/overview-emulator)
for the current feature and fidelity limitations.
