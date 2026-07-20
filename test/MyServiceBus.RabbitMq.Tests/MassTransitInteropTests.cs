using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using MassTransit;
using RabbitMQ.Client;
using TestApp;
using Testcontainers.RabbitMq;

namespace MyServiceBus.RabbitMq.Tests;

[Collection(RabbitMqInteroperabilityCollection.Name)]
public class MassTransitInteropTests
{
    [Fact]
    public async Task MyServiceBus_producer_delivers_to_MassTransit_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queueName = $"myservicebus-to-masstransit-{Guid.NewGuid():N}";
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
            configurator.ReceiveEndpoint(queueName, endpoint =>
            {
                endpoint.Consumer(() => new MassTransitConsumer(received));
            });
        });

        await bus.StartAsync();
        try
        {
            var transportFactory = CreateTransportFactory(container);
            var serializer = new EnvelopeMessageSerializer();
            var sendContext = new RabbitMqPublishContext([typeof(CrossLanguageMessage)], serializer);
            var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(CrossLanguageMessage));
            var sendTransport = await transportFactory.GetSendTransport(new Uri($"exchange:{exchangeName}"));

            await sendTransport.Send(
                new CrossLanguageMessage { Value = "myservicebus-to-masstransit" },
                sendContext);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("myservicebus-to-masstransit", message.Value);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    [Fact]
    public async Task MassTransit_producer_delivers_to_MyServiceBus_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var transportFactory = CreateTransportFactory(container);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(CrossLanguageMessage));
        var queueName = $"masstransit-to-myservicebus-{Guid.NewGuid():N}";
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
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
        });
        await bus.StartAsync();
        try
        {
            await bus.Publish(new CrossLanguageMessage { Value = "masstransit-to-myservicebus" });

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("masstransit-to-myservicebus", message.Value);
        }
        finally
        {
            await bus.StopAsync();
            await receiveTransport.Stop();
        }
    }

    [CrossLanguageFact]
    public async Task Java_MyServiceBus_producer_delivers_to_MassTransit_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queueName = $"java-to-masstransit-{Guid.NewGuid():N}";
        var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(CrossLanguageMessage));
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
            configurator.ReceiveEndpoint(queueName, endpoint =>
            {
                endpoint.Consumer(() => new MassTransitConsumer(received));
            });
        });

        await bus.StartAsync();
        try
        {
            using var javaPeer = JavaInteropPeer.Start(
                container, "produce", exchangeName, queueName, "java-to-masstransit", durableExchange: true);
            await JavaInteropPeer.WaitForOutput(javaPeer, "SENT", TimeSpan.FromMinutes(2));
            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));

            Assert.Equal(0, javaPeer.ExitCode);
            Assert.Equal("java-to-masstransit", message.Value);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    [CrossLanguageFact]
    public async Task MassTransit_producer_delivers_to_Java_MyServiceBus_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var exchangeName = MyServiceBus.EntityNameFormatter.Format(typeof(CrossLanguageMessage));
        var queueName = $"masstransit-to-java-{Guid.NewGuid():N}";
        using var javaPeer = JavaInteropPeer.Start(
            container, "consume", exchangeName, queueName, "masstransit-to-java");
        await JavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));

        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configurator =>
        {
            configurator.Host(new Uri(container.GetConnectionString()));
        });
        await bus.StartAsync();
        try
        {
            await bus.Publish(new CrossLanguageMessage { Value = "masstransit-to-java" });
            await JavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(20));
            await JavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));

            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await bus.StopAsync();
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

    private sealed class MassTransitConsumer : MassTransit.IConsumer<CrossLanguageMessage>
    {
        private readonly TaskCompletionSource<CrossLanguageMessage> received;

        public MassTransitConsumer(TaskCompletionSource<CrossLanguageMessage> received)
        {
            this.received = received;
        }

        public Task Consume(MassTransit.ConsumeContext<CrossLanguageMessage> context)
        {
            received.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }
}
