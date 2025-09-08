using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;
using Xunit.Sdk;

namespace MyServiceBus.Tests;

public class ConsumeContextAnonymousInterfaceTests
{
    public interface IOrder
    {
        int Id { get; }
    }

    class CaptureSendTransport : ISendTransport
    {
        public object? Captured;

        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
            where T : class
        {
            Captured = message;
            return Task.CompletedTask;
        }
    }

    class CaptureTransportFactory : ITransportFactory
    {
        public readonly CaptureSendTransport Transport = new();
        public Uri? Address;

        [Throws(typeof(InvalidOperationException))]
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
        {
            Address = address;
            return Task.FromResult<ISendTransport>(Transport);
        }

        [Throws(typeof(NotImplementedException))]
        public Task<IReceiveTransport> CreateReceiveTransport(EndpointDefinition definition, Func<ReceiveContext, Task> handler, Func<string?, bool>? isMessageTypeRegistered = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    [Throws(typeof(EncoderFallbackException), typeof(UriFormatException), typeof(JsonException))]
    static ConsumeContextImpl<FakeMessage> CreateContext(CaptureTransportFactory factory, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var envelope = new EnvelopeMessageContext(bytes, new Dictionary<string, object>());
        var receiveContext = new ReceiveContextImpl(envelope, null, CancellationToken.None);
        return new ConsumeContextImpl<FakeMessage>(receiveContext, factory, new SendPipe(Pipe.Empty<SendContext>()), new PublishPipe(Pipe.Empty<PublishContext>()), new EnvelopeMessageSerializer(), new Uri("rabbitmq://localhost/"), new SendContextFactory(), new PublishContextFactory());
    }

    class FakeMessage { }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(InvalidOperationException), typeof(EncoderFallbackException), typeof(JsonException), typeof(IsAssignableFromException))]
    public async Task Should_publish_anonymous_object_as_interface()
    {
        var factory = new CaptureTransportFactory();
        var ctx = CreateContext(factory, "{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{}}");

        await ctx.Publish<IOrder>(new { Id = 1 });

        var order = Assert.IsAssignableFrom<IOrder>(factory.Transport.Captured!);
        Assert.Equal(1, order.Id);
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(JsonException), typeof(EncoderFallbackException), typeof(IsAssignableFromException))]
    public async Task Should_send_anonymous_object_as_interface()
    {
        var factory = new CaptureTransportFactory();
        var ctx = CreateContext(factory, "{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{}}");

        await ctx.Send<IOrder>(new Uri("queue:orders"), new { Id = 1 });

        var order = Assert.IsAssignableFrom<IOrder>(factory.Transport.Captured!);
        Assert.Equal(1, order.Id);
        Assert.Equal(new Uri("queue:orders"), factory.Address);
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(InvalidOperationException), typeof(EncoderFallbackException), typeof(JsonException), typeof(IsAssignableFromException))]
    public async Task Should_respond_anonymous_object_as_interface()
    {
        var factory = new CaptureTransportFactory();
        var json = "{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"responseAddress\":\"queue:response\",\"message\":{}}";
        var ctx = CreateContext(factory, json);

        await ctx.RespondAsync<IOrder>(new { Id = 1 });

        var order = Assert.IsAssignableFrom<IOrder>(factory.Transport.Captured!);
        Assert.Equal(1, order.Id);
        Assert.Equal(new Uri("queue:response"), factory.Address);
    }
}
