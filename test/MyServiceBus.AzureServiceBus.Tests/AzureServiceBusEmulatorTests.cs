using Azure.Messaging.ServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus.AzureServiceBus.Tests;

[CollectionDefinition("Azure Service Bus emulator", DisableParallelization = true)]
public sealed class AzureServiceBusEmulatorCollection;

[Collection("Azure Service Bus emulator")]
public sealed class AzureServiceBusEmulatorTests
{
    private const string ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    [AzureServiceBusEmulatorFact]
    public async Task Queue_transport_round_trips_a_MassTransit_envelope()
    {
        await PurgeQueue("msb-direct");
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        configurator.UsePreProvisionedTopology();
        await using var client = new ServiceBusClient(ConnectionString);
        var factory = new AzureServiceBusTransportFactory(client, configurator);
        var received = new TaskCompletionSource<CompatibilityMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var expectedUrn = MessageUrn.For(typeof(CompatibilityMessage));
        var topology = new ReceiveEndpointTransportTopology(
            "msb-direct",
            durable: true,
            temporary: false,
            prefetchCount: 1,
            [new MessageBinding { MessageType = typeof(CompatibilityMessage), EntityName = "msb-compatibility-message" }]);
        var receiveTransport = await factory.CreateReceiveTransport(
            topology,
            context =>
            {
                if (context.TryGetMessage<CompatibilityMessage>(out var message))
                    received.TrySetResult(message);
                return Task.CompletedTask;
            },
            urn => urn == expectedUrn);

        await receiveTransport.Start();
        try
        {
            var sendContext = new SendContext(
                [typeof(CompatibilityMessage)],
                new EnvelopeMessageSerializer())
            {
                MessageId = Guid.NewGuid().ToString(),
                DestinationAddress = new Uri("sb://localhost/msb-direct")
            };
            var sendTransport = await factory.GetSendTransport(new Uri("queue:msb-direct"));
            await sendTransport.Send(new CompatibilityMessage { Value = "from-dotnet" }, sendContext);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("from-dotnet", message.Value);
        }
        finally
        {
            await receiveTransport.Stop();
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task Topic_publish_is_forwarded_to_the_endpoint_queue()
    {
        await PurgeQueue("msb-publish");
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        configurator.UsePreProvisionedTopology();
        await using var client = new ServiceBusClient(ConnectionString);
        var factory = new AzureServiceBusTransportFactory(client, configurator);
        var received = new TaskCompletionSource<CompatibilityMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var expectedUrn = MessageUrn.For(typeof(CompatibilityMessage));
        var topology = new ReceiveEndpointTransportTopology(
            "msb-publish",
            durable: true,
            temporary: false,
            prefetchCount: 1,
            [new MessageBinding { MessageType = typeof(CompatibilityMessage), EntityName = "msb-compatibility-message" }]);
        var receiveTransport = await factory.CreateReceiveTransport(
            topology,
            context =>
            {
                if (context.TryGetMessage<CompatibilityMessage>(out var message))
                    received.TrySetResult(message);
                return Task.CompletedTask;
            },
            urn => urn == expectedUrn);

        await receiveTransport.Start();
        try
        {
            var sendContext = new SendContext(
                [typeof(CompatibilityMessage)],
                new EnvelopeMessageSerializer())
            {
                MessageId = Guid.NewGuid().ToString(),
                DestinationAddress = factory.GetPublishAddress("msb-compatibility-message")
            };
            var sendTransport = await factory.GetSendTransport(sendContext.DestinationAddress);
            await sendTransport.Send(new CompatibilityMessage { Value = "published-from-dotnet" }, sendContext);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("published-from-dotnet", message.Value);
        }
        finally
        {
            await receiveTransport.Stop();
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task MassTransit_familiar_factory_configuration_publishes_to_a_handler()
    {
        await PurgeQueue("msb-publish");
        var received = new TaskCompletionSource<CompatibilityMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var bus = MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.UsePreProvisionedTopology();
            cfg.Message<CompatibilityMessage>(message =>
                message.SetEntityName("msb-compatibility-message"));
            cfg.ReceiveEndpoint("msb-publish", endpoint =>
                endpoint.Handler<CompatibilityMessage>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                }));
        });

        await bus.StartAsync(CancellationToken.None);
        try
        {
            await bus.Publish(new CompatibilityMessage { Value = "configured-dotnet-bus" });

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("configured-dotnet-bus", message.Value);
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
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

    [EntityName("msb-compatibility-message")]
    public sealed class CompatibilityMessage
    {
        public string Value { get; set; } = string.Empty;
    }
}
