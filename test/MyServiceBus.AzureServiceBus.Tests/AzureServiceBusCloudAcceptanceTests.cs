using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using MyServiceBus.Serialization;

namespace MyServiceBus.AzureServiceBus.Tests;

[Collection("Azure Service Bus emulator")]
public sealed class AzureServiceBusCloudAcceptanceTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CLOUD_CONNECTION_STRING")!;

    [AzureServiceBusCloudFact]
    public async Task Csharp_create_mode_provisions_publishes_and_consumes()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"msb-cloud-csharp-{suffix}";
        var topicName = $"msb-cloud-message-{suffix}";
        var received = new TaskCompletionSource<CloudMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        var bus = MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.Message<CloudMessage>(message => message.SetEntityName(topicName));
            cfg.ReceiveEndpoint(queueName, endpoint =>
                endpoint.Handler<CloudMessage>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                }));
        });

        try
        {
            await bus.StartAsync(CancellationToken.None);

            Assert.True((await administrationClient.QueueExistsAsync(queueName)).Value);
            Assert.True((await administrationClient.QueueExistsAsync(queueName + "_error")).Value);
            Assert.True((await administrationClient.QueueExistsAsync(queueName + "_skipped")).Value);
            Assert.True((await administrationClient.TopicExistsAsync(topicName)).Value);
            Assert.True((await administrationClient.TopicExistsAsync(queueName + "_fault")).Value);
            var subscription = (await administrationClient.GetSubscriptionAsync(topicName, queueName)).Value;
            Assert.Equal(queueName, EntityName(subscription.ForwardTo));

            await bus.Publish(new CloudMessage { Value = "csharp-live-azure" });

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal("csharp-live-azure", message.Value);
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
            await DeleteQueueIfExists(administrationClient, queueName);
            await DeleteQueueIfExists(administrationClient, queueName + "_error");
            await DeleteQueueIfExists(administrationClient, queueName + "_skipped");
            await DeleteTopicIfExists(administrationClient, topicName);
            await DeleteTopicIfExists(administrationClient, queueName + "_fault");
        }
    }

    [AzureServiceBusCloudFact]
    public async Task Csharp_create_mode_provisions_a_temporary_request_endpoint()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"msb-request-csharp-{suffix}";
        var topicName = $"msb-request-message-{suffix}";
        var responseQueueName = $"msb-response-csharp-{suffix}";
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        var server = MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.Message<CloudRequest>(message => message.SetEntityName(topicName));
            cfg.ReceiveEndpoint(queueName, endpoint =>
                endpoint.Handler<CloudRequest>(context =>
                    context.RespondAsync(new CloudResponse
                    {
                        Value = "response-to-" + context.Message.Value
                    })));
        });
        var requestConfigurator = new AzureServiceBusFactoryConfigurator();
        requestConfigurator.Host(ConnectionString);
        requestConfigurator.Message<CloudRequest>(message => message.SetEntityName(topicName));
        requestConfigurator.SetTemporaryEndpointNameFormatter(_ => responseQueueName);
        await using var client = new ServiceBusClient(ConnectionString);
        var requestFactory = new AzureServiceBusTransportFactory(client, requestConfigurator);
        var requestClient = new GenericRequestClient<CloudRequest>(
            requestFactory,
            new EnvelopeMessageSerializer(),
            new SendContextFactory(),
            timeout: RequestTimeout.After(TimeSpan.FromSeconds(30)));

        try
        {
            await server.StartAsync(CancellationToken.None);

            var response = await requestClient.GetResponseAsync<CloudResponse>(
                new CloudRequest { Value = "csharp-live-request" });

            Assert.Equal("response-to-csharp-live-request", response.Message.Value);
            var responseQueue = (await administrationClient.GetQueueAsync(responseQueueName)).Value;
            Assert.Equal(TimeSpan.FromMinutes(5), responseQueue.AutoDeleteOnIdle);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
            await DeleteQueueIfExists(administrationClient, responseQueueName);
            await DeleteQueueIfExists(administrationClient, queueName);
            await DeleteQueueIfExists(administrationClient, queueName + "_error");
            await DeleteQueueIfExists(administrationClient, queueName + "_skipped");
            await DeleteTopicIfExists(administrationClient, topicName);
            await DeleteTopicIfExists(administrationClient, queueName + "_fault");
        }
    }

    [AzureServiceBusCloudFact]
    public async Task Csharp_receiver_renews_the_lock_during_long_processing()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"msb-lock-csharp-{suffix}";
        var topicName = $"msb-lock-message-{suffix}";
        var attempts = 0;
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        await administrationClient.CreateQueueAsync(new CreateQueueOptions(queueName)
        {
            LockDuration = TimeSpan.FromSeconds(5)
        });
        var bus = MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.Message<CloudMessage>(message => message.SetEntityName(topicName));
            cfg.ReceiveEndpoint(queueName, endpoint =>
                endpoint.Handler<CloudMessage>(async _ =>
                {
                    Interlocked.Increment(ref attempts);
                    await Task.Delay(TimeSpan.FromSeconds(12));
                    processed.TrySetResult();
                }));
        });

        try
        {
            await bus.StartAsync(CancellationToken.None);
            await bus.Publish(new CloudMessage { Value = "csharp-lock-renewal" });

            await processed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await WaitForQueueToDrain(administrationClient, queueName, TimeSpan.FromSeconds(10));

            Assert.Equal(1, Volatile.Read(ref attempts));
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
            await DeleteQueueIfExists(administrationClient, queueName);
            await DeleteQueueIfExists(administrationClient, queueName + "_error");
            await DeleteQueueIfExists(administrationClient, queueName + "_skipped");
            await DeleteTopicIfExists(administrationClient, topicName);
            await DeleteTopicIfExists(administrationClient, queueName + "_fault");
        }
    }

    private static async Task DeleteQueueIfExists(
        ServiceBusAdministrationClient administrationClient,
        string queueName)
    {
        if ((await administrationClient.QueueExistsAsync(queueName)).Value)
            await administrationClient.DeleteQueueAsync(queueName);
    }

    private static async Task DeleteTopicIfExists(
        ServiceBusAdministrationClient administrationClient,
        string topicName)
    {
        if ((await administrationClient.TopicExistsAsync(topicName)).Value)
            await administrationClient.DeleteTopicAsync(topicName);
    }

    private static async Task WaitForQueueToDrain(
        ServiceBusAdministrationClient administrationClient,
        string queueName,
        TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!cancellation.IsCancellationRequested)
        {
            var properties = (await administrationClient.GetQueueRuntimePropertiesAsync(
                queueName,
                cancellation.Token)).Value;
            if (properties.ActiveMessageCount == 0)
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellation.Token);
        }

        throw new TimeoutException($"Azure Service Bus queue '{queueName}' did not drain within {timeout}.");
    }

    private static string EntityName(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri)
            ? Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'))
            : address;

    public sealed class CloudMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class CloudRequest
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class CloudResponse
    {
        public string Value { get; set; } = string.Empty;
    }
}
