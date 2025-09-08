using NSubstitute;
using RabbitMQ.Client;
using MyServiceBus;
using MyServiceBus.Topology;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace MyServiceBus.RabbitMq.Tests;

public class RabbitMqTransportFactoryTests
{
    class TestRabbitMqFactoryConfigurator : IRabbitMqFactoryConfigurator
    {
        public IEndpointNameFormatter? EndpointNameFormatter => null;
        public string ClientHost => "localhost";
        public ushort PrefetchCount { get; private set; }
        public void Message<T>(Action<MessageConfigurator> configure) { }
        public void ReceiveEndpoint(string queueName, Action<ReceiveEndpointConfigurator> configure) { }
        public void Host(string host, Action<IRabbitMqHostConfigurator>? configure = null) { }
        public void SetEndpointNameFormatter(IEndpointNameFormatter formatter) { }
        public void SetPrefetchCount(ushort prefetchCount) => PrefetchCount = prefetchCount;
    }
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
        var transportFactory = new RabbitMqTransportFactory(provider, new TestRabbitMqFactoryConfigurator());

        var topology = new ReceiveEndpointTopology
        {
            QueueName = "submit-order-queue",
            ExchangeName = "submit-order-exchange",
            RoutingKey = string.Empty
        };

        await transportFactory.CreateReceiveTransport(topology, _ => Task.CompletedTask, null);

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
        await channel.Received(1).QueueDeclareAsync(
            "submit-order-queue_skipped",
            Arg.Is(true),
            Arg.Is(false),
            Arg.Is(false),
            Arg.Any<IDictionary<string, object?>?>(),
            Arg.Is(false),
            Arg.Is(false),
            Arg.Any<CancellationToken>());
        await channel.Received(1).QueueDeclareAsync(
            "submit-order-queue_fault",
            Arg.Is(true),
            Arg.Is(false),
            Arg.Is(false),
            Arg.Any<IDictionary<string, object?>?>(),
            Arg.Is(false),
            Arg.Is(false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Throws(typeof(Exception))]
    public async Task Passes_queue_arguments_to_queue_declare()
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
        var transportFactory = new RabbitMqTransportFactory(provider, new TestRabbitMqFactoryConfigurator());

        var topology = new ReceiveEndpointTopology
        {
            QueueName = "submit-order-queue",
            ExchangeName = "submit-order-exchange",
            RoutingKey = string.Empty,
            QueueArguments = new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }
        };

        await transportFactory.CreateReceiveTransport(topology, _ => Task.CompletedTask, null);

        mainQueueArgs.ShouldNotBeNull();
        mainQueueArgs!["x-queue-type"].ShouldBe("quorum");
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
        var transportFactory = new RabbitMqTransportFactory(provider, new TestRabbitMqFactoryConfigurator());

        var topology = new ReceiveEndpointTopology
        {
            QueueName = "resp-temp",
            ExchangeName = "resp-temp",
            RoutingKey = string.Empty,
            Durable = false,
            AutoDelete = true
        };

        await transportFactory.CreateReceiveTransport(topology, _ => Task.CompletedTask, null);

        await channel.DidNotReceive().QueueDeclareAsync(
            "resp-temp_error",
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IDictionary<string, object?>?>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
        await channel.DidNotReceive().QueueDeclareAsync(
            "resp-temp_skipped",
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IDictionary<string, object?>?>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
        await channel.DidNotReceive().QueueDeclareAsync(
            "resp-temp_fault",
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IDictionary<string, object?>?>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Throws(typeof(OverflowException), typeof(InvalidOperationException), typeof(OperationCanceledException), typeof(UriFormatException))]
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
        var transportFactory = new RabbitMqTransportFactory(provider, new TestRabbitMqFactoryConfigurator());

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
    [Throws(typeof(OverflowException), typeof(InvalidOperationException), typeof(OperationCanceledException), typeof(UriFormatException))]
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
        var transportFactory = new RabbitMqTransportFactory(provider, new TestRabbitMqFactoryConfigurator());

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

    [Fact]
    public async Task Uses_global_prefetch_count()
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
        var cfg = new TestRabbitMqFactoryConfigurator();
        cfg.SetPrefetchCount(10);
        var transportFactory = new RabbitMqTransportFactory(provider, cfg);

        var topology = new ReceiveEndpointTopology
        {
            QueueName = "orders",
            ExchangeName = "orders",
            RoutingKey = string.Empty
        };

        await transportFactory.CreateReceiveTransport(topology, _ => Task.CompletedTask, null);

        await channel.Received(1).BasicQosAsync(0, (ushort)10, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Endpoint_prefetch_overrides_global()
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
        var cfg = new TestRabbitMqFactoryConfigurator();
        cfg.SetPrefetchCount(5);
        var transportFactory = new RabbitMqTransportFactory(provider, cfg);

        var topology = new ReceiveEndpointTopology
        {
            QueueName = "orders",
            ExchangeName = "orders",
            RoutingKey = string.Empty,
            PrefetchCount = 20
        };

        await transportFactory.CreateReceiveTransport(topology, _ => Task.CompletedTask, null);

        await channel.Received(1).BasicQosAsync(0, (ushort)20, false, Arg.Any<CancellationToken>());
    }
}
