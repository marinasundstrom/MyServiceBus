using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Shouldly;
using Xunit;

public class MultipleConsumerQueueTests
{
    class MyMessage { }

    class ConsumerA : IConsumer<MyMessage>
    {
        public Task Consume(ConsumeContext<MyMessage> context) => Task.CompletedTask;
    }

    class ConsumerB : IConsumer<MyMessage>
    {
        public Task Consume(ConsumeContext<MyMessage> context) => Task.CompletedTask;
    }

    class CapturingTransportFactory : ITransportFactory
    {
        public readonly List<string> Queues = new();

        [Throws(typeof(InvalidOperationException))]
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => Task.FromResult<ISendTransport>(new NopSendTransport());

        public Task<IReceiveTransport> CreateReceiveTransport(
            EndpointDefinition definition,
            Func<ReceiveContext, Task> handler,
            Func<string?, bool>? isMessageTypeRegistered = null,
            CancellationToken cancellationToken = default)
        {
            Queues.Add(definition.Address);
            return Task.FromResult<IReceiveTransport>(new NopReceiveTransport());
        }

        class NopSendTransport : ISendTransport
        {
            public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default) where T : class
                => Task.CompletedTask;
        }

        class NopReceiveTransport : IReceiveTransport
        {
            public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task Stop(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }

    [Fact]
    [Throws(typeof(UriFormatException))]
    public async Task Allows_multiple_consumers_on_distinct_queues()
    {
        var factory = new CapturingTransportFactory();
        var services = new ServiceCollection();
        services.AddSingleton<ITransportFactory>(factory);
        services.AddSingleton<ISendPipe>(_ => new SendPipe(Pipe.Empty<SendContext>()));
        services.AddSingleton<IPublishPipe>(_ => new PublishPipe(Pipe.Empty<PublishContext>()));
        services.AddSingleton<IMessageSerializer, EnvelopeMessageSerializer>();
        services.AddSingleton<ISendContextFactory, SendContextFactory>();
        services.AddSingleton<IPublishContextFactory, PublishContextFactory>();
        services.AddSingleton<TopologyRegistry>();

        var provider = services.BuildServiceProvider();
        var bus = new MessageBus(
            factory,
            provider,
            provider.GetRequiredService<ISendPipe>(),
            provider.GetRequiredService<IPublishPipe>(),
            provider.GetRequiredService<IMessageSerializer>(),
            new Uri("rabbitmq://localhost/"),
            provider.GetRequiredService<ISendContextFactory>(),
            provider.GetRequiredService<IPublishContextFactory>());

        var registry = provider.GetRequiredService<TopologyRegistry>();
        registry.RegisterConsumer<ConsumerA>("queueA", null, typeof(MyMessage));
        registry.RegisterConsumer<ConsumerB>("queueB", null, typeof(MyMessage));

        await bus.AddConsumer<MyMessage, ConsumerA>(registry.Consumers[0], null);
        await bus.AddConsumer<MyMessage, ConsumerB>(registry.Consumers[1], null);

        factory.Queues.ShouldBe(new[] { "queueA", "queueB" });
    }
}
