using NSubstitute;
using RabbitMQ.Client;
using MyServiceBus;
using MyServiceBus.Topology;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace MyServiceBus.RabbitMq.Tests;

public class RabbitMqTransportFactoryTests
{
    [Fact]
    [Throws(typeof(Exception))]
    public async Task Declares_error_exchange_and_queue()
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

        mainQueueArgs.ShouldBeNull();
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

    [Fact]
    [Throws(typeof(ObjectDisposedException), typeof(OperationCanceledException))]
    public async Task Does_not_declare_error_queue_for_autodelete_endpoints()
    {
        var channel = Substitute.For<IChannel>();

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
            .Returns(Task.FromResult(new QueueDeclareOk("ignored", 0, 0)));

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
            QueueName = "resp-temp",
            ExchangeName = "resp-temp",
            RoutingKey = string.Empty,
            Durable = false,
            AutoDelete = true
        };

        await transportFactory.CreateReceiveTransport(topology, _ => Task.CompletedTask);

        await channel.DidNotReceive().QueueDeclareAsync(
            "resp-temp_error",
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IDictionary<string, object?>?>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Throws(typeof(OverflowException), typeof(InvalidOperationException), typeof(ArgumentException), typeof(OperationCanceledException), typeof(UriFormatException))]
    public async Task Supports_exchange_scheme_uri()
    {
        var channel = Substitute.For<IChannel>();
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

        var connection = Substitute.For<IConnection>();
        connection.IsOpen.Returns(true);
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channel));

        var factory = Substitute.For<IConnectionFactory>();
        factory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));

        var provider = new ConnectionProvider(factory);
        var transportFactory = new RabbitMqTransportFactory(provider);

        await transportFactory.GetSendTransport(new Uri("exchange:orders"));

        await channel.Received(1).ExchangeDeclareAsync(
            "orders",
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IDictionary<string, object?>?>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Throws(typeof(OverflowException), typeof(InvalidOperationException), typeof(ArgumentException), typeof(OperationCanceledException), typeof(UriFormatException))]
    public async Task Supports_queue_scheme_uri()
    {
        var channel = Substitute.For<IChannel>();
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
            .Returns(Task.FromResult(new QueueDeclareOk("ignored", 0, 0)));
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

        await transportFactory.GetSendTransport(new Uri("queue:orders"));

        await channel.Received(1).QueueDeclareAsync(
            "orders",
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IDictionary<string, object?>?>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }
}
