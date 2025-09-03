using NSubstitute;
using RabbitMQ.Client;
using MyServiceBus;
using MyServiceBus.Topology;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace MyServiceBus.Tests;

public class RabbitMqTransportFactoryTests
{
    [Fact]
    [Throws(typeof(Exception))]
    public async Task Declares_dead_letter_exchange_and_queue()
    {
        var channel = Substitute.For<IChannel>();
        IDictionary<string, object?>? mainQueueArgs = null;

        channel.ExchangeDeclareAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IDictionary<string, object?>?>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        channel.QueueDeclareAsync(
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IDictionary<string, object?>?>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var queue = callInfo.ArgAt<string>(0);
                var arguments = callInfo.ArgAt<IDictionary<string, object?>?>(4);
                if (queue == "submit-order-queue")
                    mainQueueArgs = arguments;

                return Task.FromResult(new QueueDeclareOk("ignored", 0, 0));
            });

        channel.QueueBindAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IDictionary<string, object?>?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var connection = Substitute.For<IConnection>();
        connection.IsOpen.Returns(true);
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channel));

        var factory = Substitute.For<IConnectionFactory>();
        factory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));

        var provider = new ConnectionProvider(factory);
        var transportFactory = new RabbitMqTransportFactory(provider);

        var topology = new ReceiveEndpointTopology
        {
            QueueName = "submit-order-queue",
            ExchangeName = "submit-order-exchange",
            RoutingKey = string.Empty
        };

        await transportFactory.CreateReceiveTransport(topology, _ => Task.CompletedTask);

        mainQueueArgs.ShouldNotBeNull();
        mainQueueArgs!["x-dead-letter-exchange"].ShouldBe("submit-order-queue_error");
        await channel.Received(1).QueueDeclareAsync(
            "submit-order-queue_error",
            Arg.Is(true),
            Arg.Is(false),
            Arg.Is(false),
            Arg.Any<IDictionary<string, object?>?>(),
            Arg.Is(false),
            Arg.Is(false),
            Arg.Any<CancellationToken>());
    }
}
