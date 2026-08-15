using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MyServiceBus.Topology;

namespace MyServiceBus.Tests;

public class ServiceBusHostedServiceTests
{
    [Fact]
    public async Task Applies_post_build_configuration_before_resolving_bus()
    {
        var configured = false;
        var bus = new StubMessageBus();
        var services = new ServiceCollection();

        services.AddSingleton<IPostBuildAction>(new StubPostBuildAction(() => configured = true));
        services.AddSingleton<IMessageBus>(_ =>
        {
            Assert.True(configured);
            return bus;
        });

        await using var provider = services.BuildServiceProvider();
        var hostedService = new ServiceBusHostedService(
            provider,
            NullLogger<ServiceBusHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);

        Assert.True(bus.Started);
        Assert.True(bus.Stopped);
    }

    private sealed class StubPostBuildAction(Action execute) : IPostBuildAction
    {
        public void Execute(IServiceProvider provider) => execute();
    }

    private sealed class StubMessageBus : IMessageBus
    {
        public Uri Address => new("loopback://localhost/");
        public IBusTopology Topology { get; } = new TopologyRegistry();
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped = true;
            return Task.CompletedTask;
        }

        public Task Publish<T>(T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task Publish<T>(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public IPublishEndpoint GetPublishEndpoint() => this;

        public Task<ISendEndpoint> GetSendEndpoint(Uri uri) => throw new NotSupportedException();

        public Task AddConsumer<TMessage, TConsumer>(ConsumerTopology consumer, Delegate? configure = null, CancellationToken cancellationToken = default)
            where TMessage : class
            where TConsumer : class, IConsumer<TMessage> => throw new NotSupportedException();

        public Task AddHandler<TMessage>(string queueName, string exchangeName, Func<ConsumeContext<TMessage>, Task> handler, int? retryCount = null, TimeSpan? retryDelay = null, ushort? prefetchCount = null, IDictionary<string, object?>? queueArguments = null, Serialization.IMessageSerializer? serializer = null, CancellationToken cancellationToken = default)
            where TMessage : class => throw new NotSupportedException();
    }
}
