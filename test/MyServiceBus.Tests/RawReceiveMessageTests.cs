using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;

namespace MyServiceBus.Tests;

public class RawReceiveMessageTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    class TestMessage
    {
        public string Text { get; set; } = string.Empty;
    }

    class TestConsumer : IConsumer<TestMessage>
    {
        public static TaskCompletionSource<string> Received { get; set; } = new();

        public Task Consume(ConsumeContext<TestMessage> context)
        {
            Received.TrySetResult(context.Message.Text);
            return Task.CompletedTask;
        }
    }

    class StubTransportFactory : ITransportFactory
    {
        public Func<ReceiveContext, Task>? Handler { get; private set; }
        public Func<string?, bool>? IsRegistered { get; private set; }

        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => Task.FromResult<ISendTransport>(new StubSendTransport());

        public Task<IReceiveTransport> CreateReceiveTransport(
            ReceiveEndpointTopology topology,
            Func<ReceiveContext, Task> handler,
            Func<string?, bool>? isMessageTypeRegistered = null,
            CancellationToken cancellationToken = default)
        {
            Handler = handler;
            IsRegistered = isMessageTypeRegistered;
            return Task.FromResult<IReceiveTransport>(new StubReceiveTransport());
        }
    }

    class StubSendTransport : ISendTransport
    {
        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
            where T : class
            => Task.CompletedTask;
    }

    class StubReceiveTransport : IReceiveTransport
    {
        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Stop(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Handler_with_raw_serializer_accepts_raw_json()
    {
        var factory = new StubTransportFactory();
        var services = new ServiceCollection();
        services.AddSingleton<ITransportFactory>(factory);
        services.AddSingleton<ISendPipe>(_ => new SendPipe(Pipe.Empty<SendContext>()));
        services.AddSingleton<IPublishPipe>(_ => new PublishPipe(Pipe.Empty<PublishContext>()));
        services.AddSingleton<ISendContextFactory, SendContextFactory>();
        services.AddSingleton<IPublishContextFactory, PublishContextFactory>();
        services.AddSingleton<IMessageSerializer, EnvelopeMessageSerializer>();
        var provider = services.BuildServiceProvider();
        var bus = new MessageBus(
            factory,
            provider,
            provider.GetRequiredService<ISendPipe>(),
            provider.GetRequiredService<IPublishPipe>(),
            provider.GetRequiredService<IMessageSerializer>(),
            new Uri("loopback://localhost/"),
            provider.GetRequiredService<ISendContextFactory>(),
            provider.GetRequiredService<IPublishContextFactory>());

        string? received = null;
        await bus.AddHandler<TestMessage>(
            "input",
            "input",
            context =>
            {
                received = context.Message.Text;
                return Task.CompletedTask;
            },
            serializer: new RawJsonMessageSerializer());

        Assert.NotNull(factory.IsRegistered);
        Assert.True(factory.IsRegistered!(null));

        var context = new ReceiveContextImpl(
            new RawJsonMessageContext(
                Encoding.UTF8.GetBytes("{\"text\":\"hi\"}"),
                new Dictionary<string, object> { ["content_type"] = "application/json" }));

        await factory.Handler!(context);

        Assert.Equal("hi", received);
    }

    [Fact]
    public async Task Consumer_with_raw_serializer_accepts_raw_json()
    {
        var factory = new StubTransportFactory();
        var services = new ServiceCollection();
        services.AddSingleton<ITransportFactory>(factory);
        services.AddSingleton<ISendPipe>(_ => new SendPipe(Pipe.Empty<SendContext>()));
        services.AddSingleton<IPublishPipe>(_ => new PublishPipe(Pipe.Empty<PublishContext>()));
        services.AddSingleton<ISendContextFactory, SendContextFactory>();
        services.AddSingleton<IPublishContextFactory, PublishContextFactory>();
        services.AddSingleton<IMessageSerializer, EnvelopeMessageSerializer>();
        services.AddSingleton<IConsumerFactory<TestConsumer>, DefaultConstructorConsumerFactory<TestConsumer>>();
        var provider = services.BuildServiceProvider();
        var bus = new MessageBus(
            factory,
            provider,
            provider.GetRequiredService<ISendPipe>(),
            provider.GetRequiredService<IPublishPipe>(),
            provider.GetRequiredService<IMessageSerializer>(),
            new Uri("loopback://localhost/"),
            provider.GetRequiredService<ISendContextFactory>(),
            provider.GetRequiredService<IPublishContextFactory>());

        var topology = new ConsumerTopology
        {
            ConsumerType = typeof(TestConsumer),
            QueueName = "input",
            SerializerType = typeof(RawJsonMessageSerializer),
            Bindings = new List<MessageBinding>
            {
                new()
                {
                    MessageType = typeof(TestMessage),
                    EntityName = "input"
                }
            }
        };

        TestConsumer.Received = new TaskCompletionSource<string>();

        await bus.AddConsumer<TestMessage, TestConsumer>(topology);

        Assert.NotNull(factory.IsRegistered);
        Assert.True(factory.IsRegistered!(null));

        var context = new ReceiveContextImpl(
            new RawJsonMessageContext(
                Encoding.UTF8.GetBytes("{\"text\":\"hi\"}"),
                new Dictionary<string, object> { ["content_type"] = "application/json" }));

        Assert.NotNull(factory.Handler);
        await factory.Handler!(context);

        var completedTask = await Task.WhenAny(TestConsumer.Received.Task, Task.Delay(TestTimeout));
        Assert.Same(TestConsumer.Received.Task, completedTask);
        Assert.Equal("hi", await TestConsumer.Received.Task);
    }
}
