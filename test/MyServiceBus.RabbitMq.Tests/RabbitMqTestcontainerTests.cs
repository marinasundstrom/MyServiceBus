using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace MyServiceBus.RabbitMq.Tests;

public class RabbitMqTestcontainerTests
{
    [Fact]
    public async Task Transport_round_trips_an_envelope_through_rabbitmq()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-alpine").Build();
        await container.StartAsync();

        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(container.GetConnectionString())
        };
        var configurator = new RabbitMqFactoryConfigurator();
        var transportFactory = new RabbitMqTransportFactory(
            new ConnectionProvider(connectionFactory),
            configurator);

        var suffix = Guid.NewGuid().ToString("N");
        var exchangeName = $"compatibility-message-{suffix}";
        var queueName = $"compatibility-message-{suffix}";
        var expectedUrn = MessageUrn.For(typeof(CompatibilityMessage));
        var received = new TaskCompletionSource<CompatibilityMessage>(
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
                if (context.TryGetMessage<CompatibilityMessage>(out var message))
                    received.TrySetResult(message);

                return Task.CompletedTask;
            },
            messageType => messageType == expectedUrn);

        await receiveTransport.Start();
        try
        {
            var serializer = new EnvelopeMessageSerializer();
            var sendContext = new RabbitMqSendContext([typeof(CompatibilityMessage)], serializer)
            {
                DestinationAddress = new Uri($"rabbitmq://localhost/exchange/{exchangeName}")
            };
            var sendTransport = await transportFactory.GetSendTransport(
                new Uri($"exchange:{exchangeName}?durable=false&autodelete=true"));

            await sendTransport.Send(
                new CompatibilityMessage { Value = "from-dotnet" },
                sendContext);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("from-dotnet", message.Value);
        }
        finally
        {
            await receiveTransport.Stop();
        }
    }

    public sealed class CompatibilityMessage
    {
        public string Value { get; set; } = string.Empty;
    }
}
