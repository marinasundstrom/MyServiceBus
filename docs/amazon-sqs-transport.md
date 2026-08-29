# Amazon SQS and SNS Transport

The Amazon transport uses standard Amazon SQS queues for directed delivery and Amazon SNS topics for publication. Matching C# and Java adapters share the same entity names, MassTransit JSON envelope, raw SNS delivery, settlement rules, and companion destinations.

Install `Sundstrom.MyServiceBus.AmazonSqs` for C# or `io.github.marinasundstrom.myservicebus:myservicebus-amazon-sqs` for Java.

## Configuration

The cloud configuration uses the normal AWS SDK credential provider chain. Keep credentials outside application configuration and select the AWS region explicitly.

```csharp
builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();
    x.UsingAmazonSqs((context, cfg) =>
    {
        cfg.Host("eu-north-1");
        cfg.SetScope("orders-prod-");
        cfg.SetWaitTimeSeconds(20);
        cfg.SetVisibilityTimeout(60);
        cfg.ConfigureEndpoints(context);
    });
});
```

```java
services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            cfg.addConsumer(SubmitOrderConsumer.class);
            cfg.using(AmazonSqsFactoryConfigurator.class, (context, aws) -> {
                aws.host("eu-north-1");
                aws.setScope("orders-prod-");
                aws.setWaitTimeSeconds(20);
                aws.setVisibilityTimeout(60);
                aws.configureEndpoints(context);
            });
        });
```

Logical addresses use `queue:<name>` or `topic:<name>`. Externally meaningful addresses use `amazonsqs://<region>/<name>` for queues and `amazonsqs://<region>/<name>?type=topic` for topics. An external address must name the region configured by the transport.

## Topology

Create mode is the default. For each durable receive endpoint, it creates:

- the endpoint SQS queue;
- `<queue>_error` and `<queue>_skipped` SQS queues;
- an SNS topic for each consumed message entity and a raw-delivery SQS subscription;
- a `<queue>_fault` SNS topic.

The queue policy is merged with existing statements and grants only each subscribed SNS topic permission to call `sqs:SendMessage`. Repeated startup is idempotent. Companion names are deterministically shortened to the SQS 80-character limit.

Call `UsePreProvisionedTopology()` in C# or `usePreProvisionedTopology()` in Java when infrastructure owns entities, subscriptions, and policies. In this mode startup resolves existing queues and topics but does not create or mutate topology. Provision the raw SNS subscriptions and queue policies before starting the service.

Typical create-mode permissions are `sqs:CreateQueue`, `sqs:GetQueueUrl`, `sqs:GetQueueAttributes`, `sqs:SetQueueAttributes`, `sqs:ReceiveMessage`, `sqs:SendMessage`, `sqs:DeleteMessage`, `sqs:ChangeMessageVisibility`, `sqs:DeleteQueue` for temporary endpoints, plus `sns:CreateTopic`, `sns:ListTopics`, `sns:Publish`, `sns:Subscribe`, and `sns:SetSubscriptionAttributes`. Pre-provisioned deployments can remove the topology-writing actions they do not use.

## Delivery behavior and limits

- Successful processing deletes the SQS message. A failed delivery is made visible immediately unless the consume pipeline has already moved it to `_error`.
- Visibility is renewed while a handler runs. Prefetch bounds locally held deliveries; the concurrent-message limit separately bounds handler execution.
- Unknown message types are copied to `_skipped` before the source is deleted. Retry exhaustion uses the normal MyServiceBus `_error` and `Fault<T>` pipeline.
- Temporary request queues are standard queues created on start and deleted on graceful stop. They are emulated rather than native auto-delete entities.
- The adapter rejects payloads over the AWS 1 MiB message limit and uses only one native message attribute for content type; envelope headers remain in the body.
- FIFO queues, message groups, deduplication, and ordered delivery are not implemented in this first slice. Entity names ending in `.fifo` are therefore invalid.
- SNS inheritance routing is not inferred by AWS. A concrete publication is sent to its configured concrete topic; consumers of base classes or interfaces require explicit publication to those entity topics.

The transport is currently experimental. Its C#↔Java behavior is covered by LocalStack integration tests, but Amazon SQS/SNS interoperability with MassTransit has not yet passed the promotion matrix.

## Local emulator

The checked-in fixture uses LocalStack `4.14.0`, the final community image that starts without an account token:

```bash
docker compose -f test/AmazonSqsLocalStack/compose.yaml up -d --wait
RUN_AMAZON_SQS_LOCALSTACK_TESTS=1 dotnet test test/MyServiceBus.AmazonSqs.Tests/MyServiceBus.AmazonSqs.Tests.csproj
RUN_AMAZON_SQS_LOCALSTACK_TESTS=1 gradle :myservicebus-amazon-sqs:test
docker compose -f test/AmazonSqsLocalStack/compose.yaml down
```

Configure `LocalstackHost()` in C# or `localstackHost()` in Java. Newer unified LocalStack images require a LocalStack auth token; the repository fixture intentionally has no account dependency. LocalStack verifies the SQS/SNS protocol path, not AWS IAM or service-operation behavior, so cloud acceptance remains a separate promotion requirement.

Like the Azure Service Bus cloud suite, this gate is manual and opt-in; routine local tests and CI skip it. Run it only when validating transport-specific behavior against AWS, using temporary, least-privilege credentials. The narrow C# and Java cases cover topology creation, direct SQS delivery, raw SNS-to-SQS forwarding, receive/delete settlement, and cleanup. Portable retry, request, serialization, and consume-pipeline behavior stays in the shared suites. Each cloud case creates uniquely named queues and topics and removes those exact resources in a `finally` block. The runner refuses an AWS root identity:

```bash
AWS_REGION=eu-north-1 ./eng/run-amazon-sqs-cloud-tests.sh
```
