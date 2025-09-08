using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;

namespace MyServiceBus.Tests;

public class ReceiveEndpointSerializerTests
{
    class CustomSerializer : IMessageSerializer
    {
        public int Calls;
        [Throws(typeof(NotSupportedException))]
        public Task<byte[]> SerializeAsync<T>(MessageSerializationContext<T> context) where T : class
        {
            Calls++;
            context.Headers["content_type"] = "application/custom";
            return Task.FromResult(Array.Empty<byte>());
        }
    }

    class StubSendTransport : ISendTransport
    {
        public string? ContentType;
        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken) where T : class
        {
            ContentType = context.Headers.TryGetValue("content_type", out var ct) ? ct?.ToString() : null;
            return Task.CompletedTask;
        }
    }

    class StubTransportFactory : ITransportFactory
    {
        public Func<ReceiveContext, Task>? Handler;
        public readonly StubSendTransport SendTransport = new();
        [Throws(typeof(InvalidOperationException))]
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => Task.FromResult<ISendTransport>(SendTransport);
        public Task<IReceiveTransport> CreateReceiveTransport(ReceiveEndpointTopology topology, Func<ReceiveContext, Task> handler, Func<string?, bool>? isMessageTypeRegistered = null, CancellationToken cancellationToken = default)
        {
            Handler = handler;
            return Task.FromResult<IReceiveTransport>(new StubReceiveTransport());
        }
    }

    class StubReceiveTransport : IReceiveTransport
    {
        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Stop(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    class TestMessage { }
    class ResponseMessage { }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(NotNullException))]
    public async Task Handler_uses_custom_serializer_for_publish()
    {
        var services = new ServiceCollection();
        var factory = new StubTransportFactory();
        services.AddSingleton<ITransportFactory>(factory);
        services.AddSingleton<ISendPipe>(_ => new SendPipe(new PipeConfigurator<SendContext>().Build()));
        services.AddSingleton<IPublishPipe>(_ => new PublishPipe(new PipeConfigurator<PublishContext>().Build()));
        services.AddSingleton<ISendContextFactory, SendContextFactory>();
        services.AddSingleton<IPublishContextFactory, PublishContextFactory>();
        services.AddSingleton<IMessageSerializer, EnvelopeMessageSerializer>();
        var provider = services.BuildServiceProvider();
        var bus = new MessageBus(factory, provider, provider.GetRequiredService<ISendPipe>(), provider.GetRequiredService<IPublishPipe>(), provider.GetRequiredService<IMessageSerializer>(), new Uri("loopback://localhost/"), provider.GetRequiredService<ISendContextFactory>(), provider.GetRequiredService<IPublishContextFactory>());

        var serializer = new CustomSerializer();
        await bus.AddHandler<TestMessage>("input", "input", async context =>
        {
            await context.Publish(new ResponseMessage());
        }, serializer: serializer);

        var msgContext = new InMemoryMessageContext(new TestMessage(), Guid.NewGuid(), null,
            new List<string> { NamingConventions.GetMessageUrn(typeof(TestMessage)) }, new Dictionary<string, object>(), null, null, DateTimeOffset.UtcNow);
        var receiveContext = new ReceiveContextImpl(msgContext, null);
        Assert.NotNull(factory.Handler);
        await factory.Handler!(receiveContext);

        Assert.Equal("application/custom", factory.SendTransport.ContentType);
        Assert.Equal(1, serializer.Calls);
    }

    class InMemoryMessageContext : IMessageContext
    {
        readonly object _message;
        public InMemoryMessageContext(object message, Guid messageId, Guid? correlationId, IList<string> messageType, IDictionary<string, object> headers, Uri? response, Uri? fault, DateTimeOffset sent)
        {
            _message = message;
            MessageId = messageId;
            CorrelationId = correlationId;
            MessageType = messageType;
            Headers = headers;
            ResponseAddress = response;
            FaultAddress = fault;
            SentTime = sent;
        }
        public Guid MessageId { get; }
        public Guid? CorrelationId { get; }
        public IList<string> MessageType { get; }
        public Uri? ResponseAddress { get; }
        public Uri? FaultAddress { get; }
        public IDictionary<string, object> Headers { get; }
        public DateTimeOffset SentTime { get; }
        [Throws(typeof(InvalidOperationException), typeof(ObjectDisposedException))]
        public bool TryGetMessage<T>(out T? message) where T : class
        {
            if (_message is T t)
            {
                message = t;
                return true;
            }
            message = null;
            return false;
        }
    }
}
