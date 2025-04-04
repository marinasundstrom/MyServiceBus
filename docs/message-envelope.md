# Message envelope

This is the format of the envelope that MassTransit wraps the messages in.

It is either serialized as either pure JSON or BSON before being put on the transport.

### 📦 Message Envelope Fields

| **Property**         | **Type**                     | **Description**                                                                 | **Set**       |
|----------------------|------------------------------|---------------------------------------------------------------------------------|---------------|
| `messageId`          | `Guid`                       | Unique identifier for the message instance. Used for traceability and de-duplication. | **Y**         |
| `correlationId`      | `Guid?`                      | Correlates the message with a specific context, such as a saga or workflow.     | Optional      |
| `requestId`          | `Guid?`                      | Identifies a request to match with a later response.                            | **R**         |
| `initiatorId`        | `Guid?`                      | ID of the message that initiated the current flow.                              | Optional      |
| `conversationId`     | `Guid?`                      | Groups related messages into a logical conversation.                            | **Y**         |
| `sourceAddress`      | `string (URI)`               | Logical address of the sender (e.g., `queue:orders-service`).                   | **Y**         |
| `destinationAddress` | `string (URI)`               | Logical address where the message should be delivered.                          | **Y**         |
| `responseAddress`    | `string (URI)`               | Address where a consumer should send a response (used in request/response).     | **R**         |
| `faultAddress`       | `string (URI)`               | Address to send fault messages if processing fails.                             | Optional      |
| `expirationTime`     | `ISO-8601 datetime`          | Time when the message should expire and be discarded.                           | **S**         |
| `sentTime`           | `ISO-8601 datetime`          | When the message was sent.                                                      | **Y**         |
| `messageType`        | `List<string>`               | URNs representing the message’s contract or .NET type.                          | **Y**         |
| `message`            | `TMessage`                   | The actual business data payload.                                               | Required      |
| `headers`            | `Dictionary<string, object>` | Custom key-value metadata, like tenant ID, auth context, etc.                   | Optional      |
| `host`               | `HostInfo`                   | Metadata about the sending application, process, and environment.               | **Y**         |
| `contentType`        | `string`                     | Format of the message payload (e.g., `application/json`).                       | **Y**         |

---

### 🖥️ `HostInfo` Sub-Object Fields

| **Property**             | **Type**   | **Description**                                             |
|--------------------------|------------|-------------------------------------------------------------|
| `machineName`            | `string`   | Hostname of the sending machine.                            |
| `processName`            | `string`   | Name of the application process.                            |
| `processId`              | `int`      | PID of the process.                                         |
| `assembly`               | `string`   | Application assembly name.                                  |
| `assemblyVersion`        | `string`   | Version of the assembly.                                    |
| `frameworkVersion`       | `string`   | .NET runtime version.                                       |
| `massTransitVersion`     | `string`   | Version of MassTransit (or your custom bus).                |
| `operatingSystemVersion` | `string`   | OS version of the sending environment.                      |

Perfect — that sample JSON matches your schema and confirms how MassTransit structures its envelopes in real usage.

To complete your documentation, here’s how everything ties together:

---

## 📦 MassTransit Message Envelope Example

Below is a **realistic JSON message envelope** as used by MassTransit when publishing or sending messages via RabbitMQ:

```json
{
  "messageId": "181c0000-6393-3630-36a4-08daf4e7c6da",
  "requestId": "ef375b18-69ee-4a9e-b5ec-44ee1177a27e",
  "correlationId": null,
  "conversationId": null,
  "initiatorId": null,
  "sourceAddress": "rabbitmq://localhost/source",
  "destinationAddress": "rabbitmq://localhost/destination",
  "responseAddress": "rabbitmq://localhost/response",
  "faultAddress": "rabbitmq://localhost/fault",
  "messageType": [
    "urn:message:Company.Project:SubmitOrder"
  ],
  "message": {
    "orderId": "181c0000-6393-3630-36a4-08daf4e7c6da",
    "timestamp": "2023-01-12T21:55:53.714Z"
  },
  "expirationTime": null,
  "sentTime": "2023-01-12T21:55:53.715882Z",
  "headers": {
    "Application-Header": "SomeValue"
  },
  "host": {
    "machineName": "MyComputer",
    "processName": "dotnet",
    "processId": 427,
    "assembly": "TestProject",
    "assemblyVersion": "2.11.1.93",
    "frameworkVersion": "6.0.7",
    "massTransitVersion": "8.0.10.0",
    "operatingSystemVersion": "Unix 12.6.2"
  }
}
```