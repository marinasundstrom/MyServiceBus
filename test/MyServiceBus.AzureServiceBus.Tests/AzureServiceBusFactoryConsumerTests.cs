using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;

namespace MyServiceBus.AzureServiceBus.Tests;

public class AzureServiceBusFactoryConsumerTests
{
    private sealed class TestMessage
    {
    }

    private sealed class TestConsumer : IConsumer<TestMessage>
    {
        public Task Consume(ConsumeContext<TestMessage> context) => Task.CompletedTask;
    }

    private sealed class CustomConsumerFactory<TConsumer> : IConsumerFactory<TConsumer>
        where TConsumer : class
    {
        public Task Send<TMessage>(
            ConsumeContext<TMessage> context,
            IPipe<ConsumerConsumeContext<TConsumer, TMessage>> next)
            where TMessage : class => Task.CompletedTask;
    }

    [Fact]
    public void Factory_endpoint_registers_typed_consumer_without_service_binding()
    {
        var registry = new TopologyRegistry();
        var bus = new TestMessageBus();
        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddSingleton<IMessageBus>(bus);
        var provider = services.BuildServiceProvider();
        var configurator = new AzureServiceBusFactoryConfigurator();

        configurator.ReceiveEndpoint("external-orders", endpoint =>
        {
            endpoint.ConcurrentMessageLimit(4);
            endpoint.Consumer<TestConsumer, TestMessage>();
        });

        var actionsField = typeof(AzureServiceBusFactoryConfigurator).GetField(
            "_endpointActions",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var actions = (IEnumerable<Action<IMessageBus, IServiceProvider>>)actionsField.GetValue(configurator)!;
        Assert.Single(actions)(bus, provider);

        var consumer = Assert.Single(registry.Consumers);
        Assert.Equal(typeof(TestConsumer), consumer.ConsumerType);
        Assert.Equal(typeof(TestMessage), consumer.Bindings[0].MessageType);
        Assert.Equal("external-orders", consumer.QueueName);
        Assert.Equal(4, consumer.ConcurrentMessageLimit);
        Assert.Same(consumer, bus.AddedConsumer);
        Assert.Null(provider.GetService<TestConsumer>());
    }

    [Fact]
    public void Factory_uses_configured_consumer_factory_type()
    {
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.SetConsumerFactory(typeof(CustomConsumerFactory<>));
        var services = new ServiceCollection();

        configurator.Configure(services);
        var provider = services.BuildServiceProvider();

        Assert.IsType<CustomConsumerFactory<TestConsumer>>(
            provider.GetRequiredService<IConsumerFactory<TestConsumer>>());
    }

    private sealed class TestMessageBus : IMessageBus
    {
        public ConsumerTopology? AddedConsumer { get; private set; }
        public Uri Address => new("loopback://localhost/");
        public IBusTopology Topology => new TopologyRegistry();
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<T>(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        public Task Publish<T>(T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        public IPublishEndpoint GetPublishEndpoint() => this;
        public Task<ISendEndpoint> GetSendEndpoint(Uri uri) => Task.FromResult<ISendEndpoint>(new StubSendEndpoint());

        public Task AddConsumer<TMessage, TConsumer>(ConsumerTopology consumer, Delegate? configure = null, CancellationToken cancellationToken = default)
            where TMessage : class
            where TConsumer : class, IConsumer<TMessage>
        {
            AddedConsumer = consumer;
            return Task.CompletedTask;
        }

        public Task AddHandler<TMessage>(string queueName, string exchangeName, Func<ConsumeContext<TMessage>, Task> handler, int? retryCount = null, TimeSpan? retryDelay = null, ushort? prefetchCount = null, IDictionary<string, object?>? queueArguments = null, IMessageSerializer? serializer = null, CancellationToken cancellationToken = default, int? concurrentMessageLimit = null)
            where TMessage : class => Task.CompletedTask;

        private sealed class StubSendEndpoint : ISendEndpoint
        {
            public Task Send<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
            public Task Send<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        }
    }
}
