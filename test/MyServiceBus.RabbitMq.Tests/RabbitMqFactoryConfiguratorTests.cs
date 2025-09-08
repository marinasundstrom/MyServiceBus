using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Topology;
using MyServiceBus.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace MyServiceBus.RabbitMq.Tests;

public class RabbitMqFactoryConfiguratorTests
{
    class MyMessage { }

    class MyConsumer : IConsumer<MyMessage>
    {
        public Task Consume(ConsumeContext<MyMessage> context) => Task.CompletedTask;
    }

    class TestMessageBus : IMessageBus
    {
        [Throws(typeof(UriFormatException))]
        public Uri Address => new("loopback://localhost/");
        public IBusTopology Topology => new TopologyRegistry();
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task Publish<T>(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        public Task Publish<T>(T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        public IPublishEndpoint GetPublishEndpoint() => this;
        public Task<ISendEndpoint> GetSendEndpoint(Uri uri) => Task.FromResult<ISendEndpoint>(new StubSendEndpoint());
        public Task AddConsumer<TMessage, TConsumer>(ConsumerTopology consumer, Delegate? configure = null, CancellationToken cancellationToken = default)
            where TConsumer : class, IConsumer<TMessage>
            where TMessage : class => Task.CompletedTask;

        public Task AddHandler<TMessage>(string queueName, string exchangeName, Func<ConsumeContext<TMessage>, Task> handler, int? retryCount = null, TimeSpan? retryDelay = null, ushort? prefetchCount = null, IDictionary<string, object?>? queueArguments = null, IMessageSerializer? serializer = null, CancellationToken cancellationToken = default) where TMessage : class => Task.CompletedTask;

        class StubSendEndpoint : ISendEndpoint
        {
            public Task Send<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
            public Task Send<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        }
    }

    class TestBusRegistrationContext : IBusRegistrationContext
    {
        public TestBusRegistrationContext(IServiceProvider provider) => ServiceProvider = provider;
        public IServiceProvider ServiceProvider { get; }
    }

    class TestRabbitMqFactoryConfigurator : IRabbitMqFactoryConfigurator
    {
        private readonly Dictionary<Type, string> _exchangeNames = new();
        public IEndpointNameFormatter? EndpointNameFormatter { get; private set; }
        public IMessageEntityNameFormatter? EntityNameFormatter { get; private set; }
        public string ClientHost => "localhost";
        public ushort PrefetchCount { get; private set; }

        public void Message<T>(Action<MessageConfigurator> configure)
        {
            configure(new MessageConfigurator(typeof(T), _exchangeNames));
        }

        public void ReceiveEndpoint(string queueName, Action<ReceiveEndpointConfigurator> configure)
        {
            configure(new ReceiveEndpointConfigurator(queueName, _exchangeNames, new List<Action<IMessageBus, IServiceProvider>>()));
        }

        public void Host(string host, Action<IRabbitMqHostConfigurator>? configure = null)
        {
        }

        public void SetEndpointNameFormatter(IEndpointNameFormatter formatter)
        {
            EndpointNameFormatter = formatter;
        }

        public void SetEntityNameFormatter(IMessageEntityNameFormatter formatter)
        {
            EntityNameFormatter = formatter;
        }

        public void SetPrefetchCount(ushort prefetchCount)
        {
            PrefetchCount = prefetchCount;
        }
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(Exception))]
    public void ReceiveEndpoint_overrides_queue_and_exchange()
    {
        var registry = new TopologyRegistry();
        registry.RegisterConsumer<MyConsumer>("original-queue", null, typeof(MyMessage));

        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddSingleton<IMessageBus, TestMessageBus>();
        var provider = services.BuildServiceProvider();
        var context = new TestBusRegistrationContext(provider);

        var configurator = new TestRabbitMqFactoryConfigurator();
        configurator.Message<MyMessage>([Throws(typeof(NotSupportedException))] (m) => m.SetEntityName("custom-exchange"));
        configurator.ReceiveEndpoint("custom-queue", [Throws(typeof(InvalidOperationException))] (e) => e.ConfigureConsumer<MyConsumer>(context));

        var def = registry.Consumers.First(c => c.ConsumerType == typeof(MyConsumer));
        Assert.Equal("custom-queue", def.Address);
        Assert.Equal("custom-exchange", def.Bindings[0].EntityName);
    }

    class StaticEntityFormatter<T> : IMessageEntityNameFormatter<T>
    {
        public string FormatEntityName() => $"formatted-{typeof(T).Name.ToLowerInvariant()}";
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(Exception))]
    public void Message_uses_entity_name_formatter()
    {
        var registry = new TopologyRegistry();
        registry.RegisterConsumer<MyConsumer>("original-queue", null, typeof(MyMessage));

        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddSingleton<IMessageBus, TestMessageBus>();
        var provider = services.BuildServiceProvider();
        var context = new TestBusRegistrationContext(provider);

        var configurator = new TestRabbitMqFactoryConfigurator();
        configurator.Message<MyMessage>(m => m.SetEntityNameFormatter(new StaticEntityFormatter<MyMessage>()));
        configurator.ReceiveEndpoint("custom-queue", e => e.ConfigureConsumer<MyConsumer>(context));

        var def = registry.Consumers.First(c => c.ConsumerType == typeof(MyConsumer));
        Assert.Equal("formatted-mymessage", def.Bindings[0].EntityName);
    }

    [Fact]
    [Throws(typeof(NotNullException), typeof(Exception))]
    public void ReceiveEndpoint_adds_message_retry()
    {
        var registry = new TopologyRegistry();
        registry.RegisterConsumer<MyConsumer>("original-queue", null, typeof(MyMessage));

        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddSingleton<IMessageBus, TestMessageBus>();
        var provider = services.BuildServiceProvider();
        var context = new TestBusRegistrationContext(provider);

        var configurator = new TestRabbitMqFactoryConfigurator();
        configurator.ReceiveEndpoint("custom-queue", [Throws(typeof(InvalidOperationException))] (e) =>
        {
            e.UseMessageRetry(r => r.Immediate(2));
            e.ConfigureConsumer<MyConsumer>(context);
        });

        var def = registry.Consumers.First(c => c.ConsumerType == typeof(MyConsumer));
        Assert.NotNull(def.ConfigurePipe);
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(Exception))]
    public void ReceiveEndpoint_sets_queue_arguments()
    {
        var registry = new TopologyRegistry();
        registry.RegisterConsumer<MyConsumer>("original-queue", null, typeof(MyMessage));

        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddSingleton<IMessageBus, TestMessageBus>();
        var provider = services.BuildServiceProvider();
        var context = new TestBusRegistrationContext(provider);

        var configurator = new TestRabbitMqFactoryConfigurator();
        configurator.ReceiveEndpoint("custom-queue", e =>
        {
            e.SetQueueArgument("x-queue-type", "quorum");
            e.ConfigureConsumer<MyConsumer>(context);
        });

        var def = registry.Consumers.First(c => c.ConsumerType == typeof(MyConsumer));
        var settings = def.TransportSettings as RabbitMqEndpointSettings;
        Assert.Equal("quorum", settings!.QueueArguments!["x-queue-type"]);
    }

    class StaticFormatter : IEndpointNameFormatter
    {
        public string Format(Type messageType) => "formatted-" + messageType.Name.ToLowerInvariant();
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(Exception))]
    public void ConfigureEndpoints_uses_formatter()
    {
        var registry = new TopologyRegistry();
        registry.RegisterConsumer<MyConsumer>("original-queue", null, typeof(MyMessage));

        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddSingleton<IMessageBus, TestMessageBus>();
        var provider = services.BuildServiceProvider();
        var context = new TestBusRegistrationContext(provider);

        var configurator = new TestRabbitMqFactoryConfigurator();
        configurator.SetEndpointNameFormatter(new StaticFormatter());
        configurator.ConfigureEndpoints(context);

        var def = registry.Consumers.First(c => c.ConsumerType == typeof(MyConsumer));
        Assert.Equal("formatted-mymessage", def.Address);
    }
}
