using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Topology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace MyServiceBus.Tests;

public class RabbitMqFactoryConfiguratorTests
{
    class MyMessage { }

    class MyConsumer : IConsumer<MyMessage>
    {
        public Task Consume(ConsumeContext<MyMessage> context) => Task.CompletedTask;
    }

    class TestMessageBus : IMessageBus
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task Publish<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        public Task AddConsumer<TMessage, TConsumer>(ConsumerTopology consumer, Delegate? configure = null, CancellationToken cancellationToken = default)
            where TConsumer : class, IConsumer<TMessage>
            where TMessage : class => Task.CompletedTask;
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

        public void Message<T>(Action<MessageConfigurator> configure)
        {
            configure(new MessageConfigurator(typeof(T), _exchangeNames));
        }

        public void ReceiveEndpoint(string queueName, Action<ReceiveEndpointConfigurator> configure)
        {
            configure(new ReceiveEndpointConfigurator(queueName, _exchangeNames));
        }

        public void Host(string host, Action<IRabbitMqHostConfigurator>? configure = null)
        {
        }

        public void SetEndpointNameFormatter(IEndpointNameFormatter formatter)
        {
            EndpointNameFormatter = formatter;
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
        configurator.Message<MyMessage>(m => m.SetEntityName("custom-exchange"));
        configurator.ReceiveEndpoint("custom-queue", [Throws(typeof(InvalidOperationException))] (e) => e.ConfigureConsumer<MyConsumer>(context));

        var def = registry.Consumers.First(c => c.ConsumerType == typeof(MyConsumer));
        Assert.Equal("custom-queue", def.QueueName);
        Assert.Equal("custom-exchange", def.Bindings[0].EntityName);
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
        Assert.Equal("formatted-mymessage", def.QueueName);
    }
}
