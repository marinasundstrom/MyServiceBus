using Azure.Messaging.ServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using TestApp;

namespace MyServiceBus.AzureServiceBus.Tests;

[Collection("Azure Service Bus emulator")]
public sealed class CrossLanguageAzureServiceBusTests
{
    private const string ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
    private const string BindingEntityName = "msb-compatibility-message";

    [AzureServiceBusEmulatorFact]
    public async Task Csharp_direct_send_delivers_to_java_consumer()
    {
        await SendFromCsharpToJava(
            "msb-direct",
            "azure-consume",
            new Uri("queue:msb-direct"),
            "direct-from-csharp");
    }

    [AzureServiceBusEmulatorFact]
    public async Task Csharp_publish_delivers_to_java_consumer()
    {
        await SendFromCsharpToJava(
            "msb-publish",
            "azure-consume",
            new Uri("topic:msb-compatibility-message"),
            "publish-from-csharp");
    }

    [AzureServiceBusEmulatorFact]
    public Task Java_direct_send_delivers_to_csharp_consumer() =>
        ReceiveFromJava("msb-direct", "azure-send", "msb-direct", "direct-from-java");

    [AzureServiceBusEmulatorFact]
    public Task Java_publish_delivers_to_csharp_consumer() =>
        ReceiveFromJava(
            "msb-publish",
            "azure-publish",
            "msb-compatibility-message",
            "publish-from-java");

    private static async Task SendFromCsharpToJava(
        string queueName,
        string javaMode,
        Uri destination,
        string expectedValue)
    {
        await PurgeQueue(queueName);
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            ConnectionString,
            javaMode,
            queueName,
            BindingEntityName,
            expectedValue);
        await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));

        await using var client = new ServiceBusClient(ConnectionString);
        var factory = CreateTransportFactory(client);
        var context = new SendContext([typeof(CrossLanguageMessage)], new EnvelopeMessageSerializer())
        {
            MessageId = Guid.NewGuid().ToString(),
            DestinationAddress = destination
        };
        var transport = await factory.GetSendTransport(destination);
        await transport.Send(new CrossLanguageMessage { Value = expectedValue }, context);

        await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(20));
        await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
        Assert.Equal(0, javaPeer.ExitCode);
    }

    private static async Task ReceiveFromJava(
        string queueName,
        string javaMode,
        string destinationEntity,
        string expectedValue)
    {
        await PurgeQueue(queueName);
        await using var client = new ServiceBusClient(ConnectionString);
        var factory = CreateTransportFactory(client);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var topology = new ReceiveEndpointTransportTopology(
            queueName,
            durable: true,
            temporary: false,
            prefetchCount: 1,
            [new MessageBinding { MessageType = typeof(CrossLanguageMessage), EntityName = BindingEntityName }]);
        var receiveTransport = await factory.CreateReceiveTransport(
            topology,
            context =>
            {
                if (context.TryGetMessage<CrossLanguageMessage>(out var message))
                    received.TrySetResult(message);
                return Task.CompletedTask;
            },
            urn => urn == MessageUrn.For(typeof(CrossLanguageMessage)));

        await receiveTransport.Start();
        try
        {
            using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
                ConnectionString,
                javaMode,
                destinationEntity,
                "unused",
                expectedValue);
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "SENT", TimeSpan.FromMinutes(2));
            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));

            Assert.Equal(0, javaPeer.ExitCode);
            Assert.Equal(expectedValue, message.Value);
        }
        finally
        {
            await receiveTransport.Stop();
        }
    }

    private static AzureServiceBusTransportFactory CreateTransportFactory(ServiceBusClient client)
    {
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        configurator.UsePreProvisionedTopology();
        return new AzureServiceBusTransportFactory(client, configurator);
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
