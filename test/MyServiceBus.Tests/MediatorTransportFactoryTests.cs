using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;
using Xunit.Sdk;

namespace MyServiceBus.Tests;

public class MediatorTransportFactoryTests
{
    class SampleMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    class ConsumerMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    class RequestMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    class ResponseMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    class SampleConsumer : IConsumer<ConsumerMessage>
    {
        public static TaskCompletionSource<ConsumerMessage> Received = new();

        [Throws(typeof(ObjectDisposedException))]
        public Task Consume(ConsumeContext<ConsumerMessage> context)
        {
            Received.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }

    class SampleHandler : Handler<ConsumerMessage>
    {
        public static TaskCompletionSource<ConsumerMessage> Received = new();

        [Throws(typeof(ObjectDisposedException))]
        public override Task Handle(ConsumerMessage message, CancellationToken cancellationToken)
        {
            Received.TrySetResult(message);
            return Task.CompletedTask;
        }
    }

    class ResponseHandler : Handler<RequestMessage, ResponseMessage>
    {
        public static TaskCompletionSource<CancellationToken> ReceivedToken = new();

        [Throws(typeof(ObjectDisposedException))]
        public override Task<ResponseMessage> Handle(RequestMessage message, CancellationToken cancellationToken)
        {
            ReceivedToken.TrySetResult(cancellationToken);
            return Task.FromResult(new ResponseMessage { Value = message.Value + "-response" });
        }
    }

    class TestConsumeContext<T> : ConsumeContext<T> where T : class
    {
        public T Message { get; }
        public CancellationToken CancellationToken { get; }
        public TaskCompletionSource<object?> Response { get; } = new();

        public TestConsumeContext(T message, CancellationToken cancellationToken)
        {
            Message = message;
            CancellationToken = cancellationToken;
        }

        [Throws(typeof(ObjectDisposedException))]
        public Task RespondAsync<TResponse>(TResponse message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
        {
            Response.TrySetResult(message);
            return Task.CompletedTask;
        }

        public Task PublishAsync<TMessage>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where TMessage : class => Task.CompletedTask;

        public Task PublishAsync<TMessage>(TMessage message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where TMessage : class => Task.CompletedTask;

        public Task<ISendEndpoint> GetSendEndpoint(Uri uri) =>
            Task.FromResult<ISendEndpoint>(new StubSendEndpoint());

        class StubSendEndpoint : ISendEndpoint
        {
            public Task Send<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task Send<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(Exception))]
    public async Task Send_Invokes_RegisteredHandler()
    {
        var factory = new MediatorTransportFactory();
        var tcs = new TaskCompletionSource<SampleMessage>();
        var topology = new ReceiveEndpointTopology
        {
            ExchangeName = "test",
            QueueName = "queue",
            RoutingKey = ""
        };

        var receive = await factory.CreateReceiveTransport(topology, [Throws(typeof(ObjectDisposedException), typeof(InvalidOperationException))] (ctx) =>
        {
            ctx.TryGetMessage<SampleMessage>(out var msg);
            tcs.SetResult(msg!);
            return Task.CompletedTask;
        });

        await receive.Start();

        // Use a URI with a path segment so the exchange can be extracted
        var send = await factory.GetSendTransport(new Uri("loopback://localhost/test"));

        var serializer = new EnvelopeMessageSerializer();
        var sendContext = new SendContext(new[] { typeof(SampleMessage) }, serializer)
        {
            MessageId = Guid.NewGuid().ToString()
        };

        await send.Send(new SampleMessage { Value = "hi" }, sendContext);

        var message = await tcs.Task;
        Assert.Equal("hi", message.Value);

        await receive.Stop();
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(Exception))]
    public async Task Publish_delivers_message_to_registered_consumer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(cfg =>
        {
            cfg.UsingMediator();
            cfg.AddConsumer<SampleConsumer>();
        });

        using var provider = services.BuildServiceProvider();

        var hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);

        SampleConsumer.Received = new TaskCompletionSource<ConsumerMessage>();

        var bus = provider.GetRequiredService<IMessageBus>();
        await bus.PublishAsync(new ConsumerMessage { Value = "hello" });

        var message = await SampleConsumer.Received.Task;
        Assert.Equal("hello", message.Value);

        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(Exception))]
    public async Task Publish_delivers_message_to_registered_handler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(cfg =>
        {
            cfg.UsingMediator();
            cfg.AddConsumer<SampleHandler>();
        });

        using var provider = services.BuildServiceProvider();

        var hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);

        SampleHandler.Received = new TaskCompletionSource<ConsumerMessage>();

        var bus = provider.GetRequiredService<IMessageBus>();
        await bus.PublishAsync(new ConsumerMessage { Value = "handler" });

        var message = await SampleHandler.Received.Task;
        Assert.Equal("handler", message.Value);

        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(Exception))]
    public async Task Handler_with_result_responds()
    {
        ResponseHandler.ReceivedToken = new TaskCompletionSource<CancellationToken>();

        using var cts = new CancellationTokenSource();
        var context = new TestConsumeContext<RequestMessage>(new RequestMessage { Value = "hi" }, cts.Token);

        await ((IConsumer<RequestMessage>)new ResponseHandler()).Consume(context);

        var response = Assert.IsType<ResponseMessage>(await context.Response.Task);
        Assert.Equal("hi-response", response.Value);

        var token = await ResponseHandler.ReceivedToken.Task;
        Assert.Equal(cts.Token, token);
    }
}
