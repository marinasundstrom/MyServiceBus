using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Shouldly;
using TestApp;

public class TestAppDashboardTests
{
    [Fact]
    public void Creates_stable_topology_snapshot()
    {
        var registry = new TopologyRegistry();
        registry.RegisterMessage<TestMessage>("test-message");
        registry.RegisterConsumer<TestConsumer>("test-queue", null, typeof(TestMessage));
        registry.Consumers[0].PrefetchCount = 8;
        registry.Consumers[0].SerializerType = typeof(RawJsonMessageSerializer);
        registry.Consumers[0].QueueArguments = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum"
        };

        var bus = CreateBus(registry);

        var snapshot = DashboardSnapshotFactory.CreateTopology(bus, new DashboardMetadata("TestApp", "rabbitmq"));

        snapshot.ServiceName.ShouldBe("TestApp");
        snapshot.TransportName.ShouldBe("rabbitmq");
        snapshot.Address.ShouldBe("rabbitmq://localhost/");
        snapshot.Messages.Count.ShouldBe(1);
        snapshot.Consumers.Count.ShouldBe(1);

        var message = snapshot.Messages[0];
        message.MessageType.ShouldBe(typeof(TestMessage).FullName);
        message.MessageUrn.ShouldBe(MessageUrn.For(typeof(TestMessage)));
        message.EntityName.ShouldBe("test-message");

        var consumer = snapshot.Consumers[0];
        consumer.ConsumerType.ShouldBe(typeof(TestConsumer).FullName);
        consumer.QueueName.ShouldBe("test-queue");
        consumer.PrefetchCount.ShouldBe(8);
        consumer.SerializerType.ShouldBe(typeof(RawJsonMessageSerializer).FullName);
        consumer.QueueArguments["x-queue-type"].ShouldBe("quorum");
        consumer.Bindings.Count.ShouldBe(1);
        consumer.Bindings[0].MessageUrn.ShouldBe(MessageUrn.For(typeof(TestMessage)));
    }

    [Fact]
    public void Creates_overview_counts_from_topology()
    {
        var registry = new TopologyRegistry();
        registry.RegisterMessage<TestMessage>("test-message");
        registry.RegisterConsumer<TestConsumer>("queue-a", null, typeof(TestMessage));
        registry.RegisterConsumer<TestConsumer>("queue-b", null, typeof(TestMessage));

        var bus = CreateBus(registry);

        var overview = DashboardSnapshotFactory.CreateOverview(bus, new DashboardMetadata("TestApp", "rabbitmq"));

        overview.MessageCount.ShouldBe(1);
        overview.ConsumerCount.ShouldBe(2);
        overview.QueueCount.ShouldBe(2);
    }

    private static MessageBus CreateBus(TopologyRegistry registry)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITransportFactory>(new NoOpTransportFactory());
        services.AddSingleton<ISendPipe>(_ => new SendPipe(Pipe.Empty<SendContext>()));
        services.AddSingleton<IPublishPipe>(_ => new PublishPipe(Pipe.Empty<PublishContext>()));
        services.AddSingleton<IMessageSerializer, EnvelopeMessageSerializer>();
        services.AddSingleton<ISendContextFactory, SendContextFactory>();
        services.AddSingleton<IPublishContextFactory, PublishContextFactory>();
        services.AddSingleton(registry);
        services.AddSingleton<IBusTopology>(registry);
        services.AddSingleton(typeof(IConsumerFactory<>), typeof(DefaultConstructorConsumerFactory<>));

        var provider = services.BuildServiceProvider();
        return new MessageBus(
            provider.GetRequiredService<ITransportFactory>(),
            provider,
            provider.GetRequiredService<ISendPipe>(),
            provider.GetRequiredService<IPublishPipe>(),
            provider.GetRequiredService<IMessageSerializer>(),
            new Uri("rabbitmq://localhost/"),
            provider.GetRequiredService<ISendContextFactory>(),
            provider.GetRequiredService<IPublishContextFactory>());
    }

    private sealed class NoOpTransportFactory : ITransportFactory
    {
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => Task.FromResult<ISendTransport>(new NoOpSendTransport());

        public Task<IReceiveTransport> CreateReceiveTransport(
            ReceiveEndpointTopology topology,
            Func<ReceiveContext, Task> handler,
            Func<string?, bool>? isMessageTypeRegistered = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReceiveTransport>(new NoOpReceiveTransport());
    }

    private sealed class NoOpSendTransport : ISendTransport
    {
        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
            where T : class
            => Task.CompletedTask;
    }

    private sealed class NoOpReceiveTransport : IReceiveTransport
    {
        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Stop(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestMessage;

    private sealed class TestConsumer : IConsumer<TestMessage>
    {
        public Task Consume(ConsumeContext<TestMessage> context) => Task.CompletedTask;
    }
}
