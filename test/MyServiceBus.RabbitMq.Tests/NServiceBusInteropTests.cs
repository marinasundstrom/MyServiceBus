using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using NServiceBus;
using NServiceBus.Transport.RabbitMQ;
using RabbitMQ.Client;
using TestApp;
using Testcontainers.RabbitMq;

namespace MyServiceBus.RabbitMq.Tests;

[Collection(RabbitMqInteroperabilityCollection.Name)]
public class NServiceBusInteropTests
{
    [Fact]
    public async Task MyServiceBus_direct_send_delivers_to_NServiceBus()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-management-alpine")
            .WithPortBinding(15672, true)
            .Build();
        await container.StartAsync();

        var queueName = $"myservicebus-to-nservicebus-{Guid.NewGuid():N}";
        NServiceBusCrossLanguageMessageHandler.Received = NewCompletionSource();
        var endpoint = await StartNServiceBusEndpoint(container, queueName);
        try
        {
            var transportFactory = CreateTransportFactory(container);
            var sendTransport = await transportFactory.GetSendTransport(new Uri($"queue:{queueName}"));
            var sendContext = new RabbitMqSendContext(
                [typeof(CrossLanguageMessage)],
                new NServiceBusJsonMessageSerializer())
            {
                MessageId = Guid.NewGuid().ToString()
            };

            await sendTransport.Send(
                new CrossLanguageMessage { Value = "myservicebus-to-nservicebus" },
                sendContext);

            var message = await NServiceBusCrossLanguageMessageHandler.Received.Task
                .WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("myservicebus-to-nservicebus", message.Value);
        }
        finally
        {
            await endpoint.Stop();
        }
    }

    [Fact]
    public async Task NServiceBus_direct_send_delivers_to_MyServiceBus()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-management-alpine")
            .WithPortBinding(15672, true)
            .Build();
        await container.StartAsync();

        var queueName = $"nservicebus-to-myservicebus-{Guid.NewGuid():N}";
        var transportFactory = CreateTransportFactory(container);
        var received = NewCompletionSource();
        var receiveTransport = await transportFactory.CreateReceiveTransport(
            new ReceiveEndpointTransportTopology(
                queueName,
                durable: true,
                temporary: false,
                prefetchCount: 1,
                [new MessageBinding
                {
                    MessageType = typeof(CrossLanguageMessage),
                    EntityName = EntityNameFormatter.Format(typeof(CrossLanguageMessage))
                }]),
            context =>
            {
                if (context.TryGetMessage<CrossLanguageMessage>(out var message))
                    received.TrySetResult(message);
                return Task.CompletedTask;
            },
            messageType => messageType == MessageUrn.For(typeof(CrossLanguageMessage)));

        await receiveTransport.Start();
        var endpoint = await StartNServiceBusEndpoint(
            container,
            $"nservicebus-sender-{Guid.NewGuid():N}");
        try
        {
            var options = new SendOptions();
            options.SetDestination(queueName);
            await endpoint.Send(
                new CrossLanguageMessage { Value = "nservicebus-to-myservicebus" },
                options);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("nservicebus-to-myservicebus", message.Value);
        }
        finally
        {
            await endpoint.Stop();
            await receiveTransport.Stop();
        }
    }

    [Fact]
    public async Task Java_MyServiceBus_direct_send_delivers_to_NServiceBus()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-management-alpine")
            .WithPortBinding(15672, true)
            .Build();
        await container.StartAsync();

        var queueName = $"java-myservicebus-to-nservicebus-{Guid.NewGuid():N}";
        NServiceBusCrossLanguageMessageHandler.Received = NewCompletionSource();
        var endpoint = await StartNServiceBusEndpoint(container, queueName);
        using var javaPeer = JavaInteropPeer.Start(
            container,
            "nservicebus-send",
            EntityNameFormatter.Format(typeof(CrossLanguageMessage)),
            queueName,
            "java-myservicebus-to-nservicebus");
        try
        {
            await JavaInteropPeer.WaitForOutput(javaPeer, "SENT", TimeSpan.FromSeconds(30));
            var message = await NServiceBusCrossLanguageMessageHandler.Received.Task
                .WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("java-myservicebus-to-nservicebus", message.Value);
            await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
            await endpoint.Stop();
        }
    }

    [Fact]
    public async Task NServiceBus_direct_send_delivers_to_Java_MyServiceBus()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-management-alpine")
            .WithPortBinding(15672, true)
            .Build();
        await container.StartAsync();

        var queueName = $"nservicebus-to-java-myservicebus-{Guid.NewGuid():N}";
        using var javaPeer = JavaInteropPeer.Start(
            container,
            "nservicebus-consume",
            EntityNameFormatter.Format(typeof(CrossLanguageMessage)),
            queueName,
            "nservicebus-to-java-myservicebus");
        await JavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromSeconds(30));
        var endpoint = await StartNServiceBusEndpoint(
            container,
            $"nservicebus-java-sender-{Guid.NewGuid():N}");
        try
        {
            var options = new SendOptions();
            options.SetDestination(queueName);
            await endpoint.Send(
                new CrossLanguageMessage { Value = "nservicebus-to-java-myservicebus" },
                options);

            await JavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(30));
            await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
            await endpoint.Stop();
        }
    }

    private static async Task<IEndpointInstance> StartNServiceBusEndpoint(
        RabbitMqContainer container,
        string endpointName)
    {
        var configuration = new EndpointConfiguration(endpointName);
        var transportExtensions = configuration.UseTransport<RabbitMQTransport>();
        transportExtensions.ConnectionString(container.GetConnectionString());
        transportExtensions.UseConventionalRoutingTopology(QueueType.Classic);
        var connectionUri = new Uri(container.GetConnectionString());
        var credentials = connectionUri.UserInfo.Split(':', 2);
        transportExtensions.ManagementApiConfiguration(
            $"http://{container.Hostname}:{container.GetMappedPublicPort(15672)}",
            Uri.UnescapeDataString(credentials[0]),
            Uri.UnescapeDataString(credentials[1]));
        configuration.UseSerialization<SystemJsonSerializer>();
        configuration.SendFailedMessagesTo(endpointName + "-error");
        configuration.EnableInstallers();
        configuration.Recoverability().Immediate(settings => settings.NumberOfRetries(0));
        configuration.Recoverability().Delayed(settings => settings.NumberOfRetries(0));
        configuration.Conventions().DefiningMessagesAs(type => type == typeof(CrossLanguageMessage));
        return await Endpoint.Start(configuration);
    }

    private static RabbitMqTransportFactory CreateTransportFactory(RabbitMqContainer container)
    {
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(container.GetConnectionString())
        };
        return new RabbitMqTransportFactory(
            new ConnectionProvider(connectionFactory),
            new RabbitMqFactoryConfigurator());
    }

    private static TaskCompletionSource<CrossLanguageMessage> NewCompletionSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class NServiceBusCrossLanguageMessageHandler : IHandleMessages<CrossLanguageMessage>
{
    public static TaskCompletionSource<CrossLanguageMessage> Received { get; set; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Handle(CrossLanguageMessage message, IMessageHandlerContext context)
    {
        Received.TrySetResult(message);
        return Task.CompletedTask;
    }
}
