using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Inspection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Shouldly;

public class BusInspectionProviderTests
{
    [Fact]
    public void Creates_endpoint_centric_snapshot()
    {
        var registry = new TopologyRegistry();
        registry.RegisterMessage<TestMessage>("test-message");
        registry.RegisterConsumer<TestConsumer>("test-queue", null, typeof(TestMessage));
        registry.Consumers[0].PrefetchCount = 8;

        var bus = CreateBus(registry);
        var provider = new BusInspectionProvider(bus);

        var snapshot = provider.GetSnapshot();

        snapshot.TransportName.ShouldBe("rabbitmq");
        snapshot.Messages.Count.ShouldBe(1);
        snapshot.ReceiveEndpoints.Count.ShouldBe(1);
        snapshot.Consumers.Count.ShouldBe(1);
        snapshot.ReceiveEndpoints[0].EndpointName.ShouldBe("test-queue");
        snapshot.ReceiveEndpoints[0].Bindings[0].MessageUrn.ShouldBe(MessageUrn.For(typeof(TestMessage)));
        snapshot.ReceiveEndpoints[0].Transport?.TransportName.ShouldBe("rabbitmq");
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
