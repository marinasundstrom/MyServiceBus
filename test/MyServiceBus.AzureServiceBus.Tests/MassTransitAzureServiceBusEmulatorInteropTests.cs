using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using MassTransit;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using TestApp;

namespace MyServiceBus.AzureServiceBus.Tests;

[Collection("Azure Service Bus emulator")]
public sealed class MassTransitAzureServiceBusEmulatorInteropTests
{
    private const string DataConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    private const string ManagementConnectionString =
        "Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    [AzureServiceBusEmulatorFact]
    public async Task Csharp_MyServiceBus_direct_send_is_consumed_by_MassTransit()
    {
        await PurgeQueue("msb-direct");
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var serviceBusClient = new ServiceBusClient(DataConnectionString);
        var administrationClient = new ServiceBusAdministrationClient(ManagementConnectionString);
        var massTransit = Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(new Uri("sb://localhost"), serviceBusClient, administrationClient);
            cfg.ReceiveEndpoint("msb-direct", endpoint =>
            {
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Handler<CrossLanguageMessage>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                });
            });
        });
        await using var myServiceBusClient = new ServiceBusClient(DataConnectionString);
        var factory = CreateMyServiceBusFactory(myServiceBusClient);

        await massTransit.StartAsync(CancellationToken.None);
        try
        {
            var sendContext = CreateSendContext(new Uri("sb://localhost/msb-direct"));
            var sendTransport = await factory.GetSendTransport(new Uri("queue:msb-direct"));
            await sendTransport.Send(
                new CrossLanguageMessage { Value = "csharp-to-masstransit" },
                sendContext);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("csharp-to-masstransit", message.Value);
        }
        finally
        {
            await massTransit.StopAsync();
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task MassTransit_direct_send_is_consumed_by_Csharp_MyServiceBus()
    {
        await PurgeQueue("msb-direct");
        await using var myServiceBusClient = new ServiceBusClient(DataConnectionString);
        var factory = CreateMyServiceBusFactory(myServiceBusClient);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var topology = new ReceiveEndpointTransportTopology(
            "msb-direct",
            durable: true,
            temporary: false,
            prefetchCount: 1,
            [new MessageBinding
            {
                MessageType = typeof(CrossLanguageMessage),
                EntityName = "msb-compatibility-message"
            }]);
        var receiveTransport = await factory.CreateReceiveTransport(
            topology,
            context =>
            {
                if (context.TryGetMessage<CrossLanguageMessage>(out var message))
                    received.TrySetResult(message);
                return Task.CompletedTask;
            },
            urn => urn == MessageUrn.For(typeof(CrossLanguageMessage)));
        await using var serviceBusClient = new ServiceBusClient(DataConnectionString);
        var administrationClient = new ServiceBusAdministrationClient(ManagementConnectionString);
        var massTransit = Bus.Factory.CreateUsingAzureServiceBus(cfg =>
            cfg.Host(new Uri("sb://localhost"), serviceBusClient, administrationClient));

        await receiveTransport.Start();
        await massTransit.StartAsync(CancellationToken.None);
        try
        {
            var endpoint = await massTransit.GetSendEndpoint(new Uri("queue:msb-direct"));
            await endpoint.Send(new CrossLanguageMessage { Value = "masstransit-to-csharp" });

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("masstransit-to-csharp", message.Value);
        }
        finally
        {
            await massTransit.StopAsync();
            await receiveTransport.Stop();
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task Csharp_MyServiceBus_publish_is_consumed_by_MassTransit()
    {
        await PurgeQueue("msb-publish");
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var serviceBusClient = new ServiceBusClient(DataConnectionString);
        var administrationClient = new ServiceBusAdministrationClient(ManagementConnectionString);
        var massTransit = Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(new Uri("sb://localhost"), serviceBusClient, administrationClient);
            cfg.ReceiveEndpoint("msb-publish", endpoint =>
            {
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Handler<CrossLanguageMessage>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                });
            });
        });
        await using var myServiceBusClient = new ServiceBusClient(DataConnectionString);
        var factory = CreateMyServiceBusFactory(myServiceBusClient);

        await massTransit.StartAsync(CancellationToken.None);
        try
        {
            var destination = factory.GetPublishAddress("msb-compatibility-message");
            var sendTransport = await factory.GetSendTransport(destination);
            await sendTransport.Send(
                new CrossLanguageMessage { Value = "csharp-publish-to-masstransit" },
                CreateSendContext(destination));

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("csharp-publish-to-masstransit", message.Value);
        }
        finally
        {
            await massTransit.StopAsync();
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task MassTransit_publish_is_consumed_by_Csharp_MyServiceBus()
    {
        await PurgeQueue("msb-publish");
        await using var myServiceBusClient = new ServiceBusClient(DataConnectionString);
        var factory = CreateMyServiceBusFactory(myServiceBusClient);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var receiveTransport = await CreateMyServiceBusReceiveTransport(
            factory,
            "msb-publish",
            message => received.TrySetResult(message));
        await using var serviceBusClient = new ServiceBusClient(DataConnectionString);
        var administrationClient = new ServiceBusAdministrationClient(ManagementConnectionString);
        var massTransit = Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(new Uri("sb://localhost"), serviceBusClient, administrationClient);
            cfg.Message<CrossLanguageMessage>(message =>
                message.SetEntityName("msb-compatibility-message"));
        });

        await receiveTransport.Start();
        await massTransit.StartAsync(CancellationToken.None);
        try
        {
            await massTransit.Publish(
                new CrossLanguageMessage { Value = "masstransit-publish-to-csharp" });

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("masstransit-publish-to-csharp", message.Value);
        }
        finally
        {
            await massTransit.StopAsync();
            await receiveTransport.Stop();
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task Java_MyServiceBus_direct_send_is_consumed_by_MassTransit()
    {
        await PurgeQueue("msb-direct");
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var serviceBusClient = new ServiceBusClient(DataConnectionString);
        var administrationClient = new ServiceBusAdministrationClient(ManagementConnectionString);
        var massTransit = Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(new Uri("sb://localhost"), serviceBusClient, administrationClient);
            cfg.ReceiveEndpoint("msb-direct", endpoint =>
            {
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Handler<CrossLanguageMessage>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                });
            });
        });

        await massTransit.StartAsync(CancellationToken.None);
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            DataConnectionString,
            "azure-send-public",
            "msb-direct",
            "unused",
            "java-to-masstransit");
        try
        {
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "SENT", TimeSpan.FromMinutes(2));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("java-to-masstransit", message.Value);
        }
        finally
        {
            await massTransit.StopAsync();
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task MassTransit_direct_send_is_consumed_by_Java_MyServiceBus()
    {
        await PurgeQueue("msb-direct");
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            DataConnectionString,
            "azure-consume-value",
            "msb-direct",
            "msb-compatibility-message",
            "masstransit-to-java");
        await using var serviceBusClient = new ServiceBusClient(DataConnectionString);
        var administrationClient = new ServiceBusAdministrationClient(ManagementConnectionString);
        var massTransit = Bus.Factory.CreateUsingAzureServiceBus(cfg =>
            cfg.Host(new Uri("sb://localhost"), serviceBusClient, administrationClient));

        try
        {
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));
            await massTransit.StartAsync(CancellationToken.None);
            var endpoint = await massTransit.GetSendEndpoint(new Uri("queue:msb-direct"));
            await endpoint.Send(new CrossLanguageMessage { Value = "masstransit-to-java" });

            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(20));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await massTransit.StopAsync();
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task Java_MyServiceBus_publish_is_consumed_by_MassTransit()
    {
        await PurgeQueue("msb-publish");
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var serviceBusClient = new ServiceBusClient(DataConnectionString);
        var administrationClient = new ServiceBusAdministrationClient(ManagementConnectionString);
        var massTransit = Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(new Uri("sb://localhost"), serviceBusClient, administrationClient);
            cfg.ReceiveEndpoint("msb-publish", endpoint =>
            {
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Handler<CrossLanguageMessage>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                });
            });
        });

        await massTransit.StartAsync(CancellationToken.None);
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            DataConnectionString,
            "azure-publish",
            "msb-compatibility-message",
            "unused",
            "java-publish-to-masstransit");
        try
        {
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "SENT", TimeSpan.FromMinutes(2));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("java-publish-to-masstransit", message.Value);
        }
        finally
        {
            await massTransit.StopAsync();
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task MassTransit_publish_is_consumed_by_Java_MyServiceBus()
    {
        await PurgeQueue("msb-publish");
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            DataConnectionString,
            "azure-consume-value",
            "msb-publish",
            "msb-compatibility-message",
            "masstransit-publish-to-java");
        await using var serviceBusClient = new ServiceBusClient(DataConnectionString);
        var administrationClient = new ServiceBusAdministrationClient(ManagementConnectionString);
        var massTransit = Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(new Uri("sb://localhost"), serviceBusClient, administrationClient);
            cfg.Message<CrossLanguageMessage>(message =>
                message.SetEntityName("msb-compatibility-message"));
        });

        try
        {
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));
            await massTransit.StartAsync(CancellationToken.None);
            await massTransit.Publish(
                new CrossLanguageMessage { Value = "masstransit-publish-to-java" });

            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(20));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await massTransit.StopAsync();
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
        }
    }

    private static AzureServiceBusTransportFactory CreateMyServiceBusFactory(ServiceBusClient client)
    {
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(DataConnectionString);
        configurator.UsePreProvisionedTopology();
        configurator.Message<CrossLanguageMessage>(message =>
            message.SetEntityName("msb-compatibility-message"));
        return new AzureServiceBusTransportFactory(client, configurator);
    }

    private static Task<IReceiveTransport> CreateMyServiceBusReceiveTransport(
        AzureServiceBusTransportFactory factory,
        string queueName,
        Action<CrossLanguageMessage> onMessage)
    {
        var topology = new ReceiveEndpointTransportTopology(
            queueName,
            durable: true,
            temporary: false,
            prefetchCount: 1,
            [new MessageBinding
            {
                MessageType = typeof(CrossLanguageMessage),
                EntityName = "msb-compatibility-message"
            }]);
        return factory.CreateReceiveTransport(
            topology,
            context =>
            {
                if (context.TryGetMessage<CrossLanguageMessage>(out var message))
                    onMessage(message);
                return Task.CompletedTask;
            },
            urn => urn == MessageUrn.For(typeof(CrossLanguageMessage)));
    }

    private static SendContext CreateSendContext(Uri destinationAddress) =>
        new([typeof(CrossLanguageMessage)], new EnvelopeMessageSerializer())
        {
            MessageId = Guid.NewGuid().ToString(),
            DestinationAddress = destinationAddress
        };

    private static async Task PurgeQueue(string queueName)
    {
        await using var client = new ServiceBusClient(DataConnectionString);
        var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
            PrefetchCount = 100
        });

        while ((await receiver.ReceiveMessagesAsync(100, TimeSpan.FromMilliseconds(250))).Count > 0)
        {
        }

        await receiver.DisposeAsync();
    }
}
