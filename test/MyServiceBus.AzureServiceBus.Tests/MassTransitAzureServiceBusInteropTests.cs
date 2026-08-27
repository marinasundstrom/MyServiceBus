using Azure.Messaging.ServiceBus;
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
        await PurgeQueue("msb-direct");
        await using var client = new ServiceBusClient(ConnectionString);
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        configurator.UsePreProvisionedTopology();
        var factory = new AzureServiceBusTransportFactory(client, configurator);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var topology = new ReceiveEndpointTransportTopology(
            "msb-direct",
            durable: true,
            temporary: false,
            prefetchCount: 1,
            [new MessageBinding { MessageType = typeof(CrossLanguageMessage), EntityName = "msb-compatibility-message" }]);
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
            var endpoint = await bus.GetSendEndpoint(new Uri("queue:msb-direct"));
            await endpoint.Send(new CrossLanguageMessage { Value = "masstransit-to-csharp" });

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("masstransit-to-csharp", message.Value);
        }
        finally
        {
            await bus.StopAsync();
            await receiveTransport.Stop();
        }
    }

    [AzureServiceBusCloudFact]
    public async Task MassTransit_send_is_consumed_by_Java_MyServiceBus()
    {
        await PurgeQueue("msb-direct");
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            ConnectionString,
            "azure-consume",
            "msb-direct",
            "msb-compatibility-message",
            "masstransit-to-java");
        await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));
        var bus = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg => cfg.Host(ConnectionString));

        using var startTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await bus.StartAsync(startTimeout.Token);
        try
        {
            var endpoint = await bus.GetSendEndpoint(new Uri("queue:msb-direct"));
            await endpoint.Send(new CrossLanguageMessage { Value = "masstransit-to-java" });

            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(20));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    private static async Task PurgeQueue(string queueName)
    {
        await using var client = new ServiceBusClient(ConnectionString);
        await using var receiver = client.CreateReceiver(
            queueName,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
        while ((await receiver.ReceiveMessagesAsync(100, TimeSpan.FromMilliseconds(250))).Count > 0)
        {
        }
    }
}
