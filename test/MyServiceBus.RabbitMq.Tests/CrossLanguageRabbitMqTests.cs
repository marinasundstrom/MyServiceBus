using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using TestApp;

namespace MyServiceBus.RabbitMq.Tests;

[Collection(RabbitMqInteroperabilityCollection.Name)]
public class CrossLanguageRabbitMqTests
{
    [CrossLanguageFact]
    public async Task Csharp_direct_send_delivers_to_java_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-alpine").Build();
        await container.StartAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var exchangeName = $"csharp-send-to-java-exchange-{suffix}";
        var queueName = $"csharp-send-to-java-{suffix}";
        const string expectedValue = "send-from-csharp";
        using var javaPeer = JavaInteropPeer.Start(container, "consume", exchangeName, queueName, expectedValue);
        await JavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));

        var transportFactory = CreateTransportFactory(container);
        var sendContext = new RabbitMqSendContext(
            [typeof(CrossLanguageMessage)],
            new EnvelopeMessageSerializer());
        var sendTransport = await transportFactory.GetSendTransport(new Uri($"queue:{queueName}"));
        await sendTransport.Send(new CrossLanguageMessage { Value = expectedValue }, sendContext);

        await JavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(20));
        await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
        Assert.Equal(0, javaPeer.ExitCode);
    }

    [CrossLanguageFact]
    public async Task Csharp_producer_delivers_to_java_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-alpine").Build();
        await container.StartAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var exchangeName = $"csharp-to-java-{suffix}";
        var queueName = exchangeName;
        const string expectedValue = "from-csharp";
        using var javaPeer = JavaInteropPeer.Start(container, "consume", exchangeName, queueName, expectedValue);

        await JavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));

        var transportFactory = CreateTransportFactory(container);
        var serializer = new EnvelopeMessageSerializer();
        var sendContext = new RabbitMqSendContext([typeof(CrossLanguageMessage)], serializer);
        var sendTransport = await transportFactory.GetSendTransport(
            new Uri($"exchange:{exchangeName}"));
        await sendTransport.Send(new CrossLanguageMessage { Value = expectedValue }, sendContext);

        await JavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(20));
        await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
        Assert.Equal(0, javaPeer.ExitCode);
    }

    [CrossLanguageFact]
    public async Task Java_producer_delivers_to_csharp_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-alpine").Build();
        await container.StartAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var exchangeName = $"java-to-csharp-{suffix}";
        var queueName = exchangeName;
        const string expectedValue = "from-java";
        var transportFactory = CreateTransportFactory(container);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var receiveTransport = await transportFactory.CreateReceiveTransport(
            new ReceiveEndpointTopology
            {
                QueueName = queueName,
                ExchangeName = exchangeName,
                Durable = false,
                AutoDelete = true
            },
            context =>
            {
                if (context.TryGetMessage<CrossLanguageMessage>(out var message))
                    received.TrySetResult(message);

                return Task.CompletedTask;
            },
            messageType => messageType == MessageUrn.For(typeof(CrossLanguageMessage)));

        await receiveTransport.Start();
        try
        {
            using var javaPeer = JavaInteropPeer.Start(container, "produce", exchangeName, queueName, expectedValue);
            await JavaInteropPeer.WaitForOutput(javaPeer, "SENT", TimeSpan.FromMinutes(2));
            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));

            Assert.Equal(0, javaPeer.ExitCode);
            Assert.Equal(expectedValue, message.Value);
        }
        finally
        {
            await receiveTransport.Stop();
        }
    }

    [CrossLanguageFact]
    public async Task Java_direct_send_delivers_to_csharp_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-alpine").Build();
        await container.StartAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var exchangeName = $"java-send-to-csharp-exchange-{suffix}";
        var queueName = $"java-send-to-csharp-{suffix}";
        const string expectedValue = "send-from-java";
        var transportFactory = CreateTransportFactory(container);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var receiveTransport = await transportFactory.CreateReceiveTransport(
            new ReceiveEndpointTopology
            {
                QueueName = queueName,
                ExchangeName = exchangeName,
                Durable = true,
                AutoDelete = false
            },
            context =>
            {
                if (context.TryGetMessage<CrossLanguageMessage>(out var message))
                    received.TrySetResult(message);

                return Task.CompletedTask;
            },
            messageType => messageType == MessageUrn.For(typeof(CrossLanguageMessage)));

        await receiveTransport.Start();
        try
        {
            using var javaPeer = JavaInteropPeer.Start(
                container, "send", exchangeName, queueName, expectedValue);
            await JavaInteropPeer.WaitForOutput(javaPeer, "SENT", TimeSpan.FromMinutes(2));
            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));

            Assert.Equal(0, javaPeer.ExitCode);
            Assert.Equal(expectedValue, message.Value);
        }
        finally
        {
            await receiveTransport.Stop();
        }
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

}
