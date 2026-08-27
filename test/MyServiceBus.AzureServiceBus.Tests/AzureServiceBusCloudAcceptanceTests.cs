using Azure.Messaging.ServiceBus.Administration;

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

    private static string EntityName(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri)
            ? Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'))
            : address;

    public sealed class CloudMessage
    {
        public string Value { get; set; } = string.Empty;
    }
}
