using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using MassTransit;
using MyServiceBus.Topology;
using TestApp;

namespace MyServiceBus.AzureServiceBus.Tests;

[Collection("Azure Service Bus emulator")]
public sealed class MassTransitAzureServiceBusInteropTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CLOUD_CONNECTION_STRING")!;

    [AzureServiceBusCloudFact]
    public async Task MassTransit_send_is_consumed_by_Csharp_MyServiceBus()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"msb-mt-csharp-{suffix}";
        var topicName = $"msb-mt-message-{suffix}";
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        await using var client = new ServiceBusClient(ConnectionString);
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        var factory = new AzureServiceBusTransportFactory(client, configurator);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var topology = new ReceiveEndpointTransportTopology(
            queueName,
            durable: true,
            temporary: false,
            prefetchCount: 1,
            [new MessageBinding { MessageType = typeof(CrossLanguageMessage), EntityName = topicName }]);
        var receiveTransport = await factory.CreateReceiveTransport(
            topology,
            context =>
            {
                if (context.TryGetMessage<CrossLanguageMessage>(out var message))
                    received.TrySetResult(message);
                return Task.CompletedTask;
            },
            urn => urn == MessageUrn.For(typeof(CrossLanguageMessage)));
        var bus = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg => cfg.Host(ConnectionString));

        await receiveTransport.Start();
        using var startTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await bus.StartAsync(startTimeout.Token);
        try
        {
            var endpoint = await bus.GetSendEndpoint(new Uri($"queue:{queueName}"));
            await endpoint.Send(new CrossLanguageMessage { Value = "masstransit-to-csharp" });

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("masstransit-to-csharp", message.Value);
        }
        finally
        {
            await bus.StopAsync();
            await receiveTransport.Stop();
            await DeleteTopology(administrationClient, queueName, topicName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task MassTransit_send_is_consumed_by_Java_MyServiceBus()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"msb-mt-java-{suffix}";
        var topicName = $"msb-mt-message-{suffix}";
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            ConnectionString,
            "azure-consume-value",
            queueName,
            topicName,
            "masstransit-to-java",
            createTopology: true);
        var bus = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg => cfg.Host(ConnectionString));

        try
        {
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));
            using var startTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await bus.StartAsync(startTimeout.Token);
            var endpoint = await bus.GetSendEndpoint(new Uri($"queue:{queueName}"));
            await endpoint.Send(new CrossLanguageMessage { Value = "masstransit-to-java" });

            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(20));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await bus.StopAsync();
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
            await DeleteTopology(administrationClient, queueName, topicName);
        }
    }

    private static async Task DeleteTopology(
        ServiceBusAdministrationClient administrationClient,
        string queueName,
        string topicName)
    {
        await DeleteQueueIfExists(administrationClient, queueName);
        await DeleteQueueIfExists(administrationClient, queueName + "_error");
        await DeleteQueueIfExists(administrationClient, queueName + "_skipped");
        await DeleteTopicIfExists(administrationClient, topicName);
        await DeleteTopicIfExists(administrationClient, queueName + "_fault");
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
}
