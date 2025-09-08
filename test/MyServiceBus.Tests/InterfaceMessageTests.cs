using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus;
using MyServiceBus.Serialization;
using Xunit;
using Xunit.Sdk;

namespace MyServiceBus.Tests;

public class InterfaceMessageTests
{
    interface ICommand { string Value { get; } }
    interface IReply { string Value { get; } }

    class CaptureSendTransport : ISendTransport
    {
        public object? Message;
        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default) where T : class
        {
            Message = message!;
            return Task.CompletedTask;
        }
    }

    class StubTransportFactory : ITransportFactory
    {
        public readonly CaptureSendTransport Transport = new();

        [Throws(typeof(InvalidOperationException))]
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => Task.FromResult<ISendTransport>(Transport);

        public Task<IReceiveTransport> CreateReceiveTransport(ReceiveEndpointTopology topology, Func<ReceiveContext, Task> handler, Func<string?, bool>? isMessageTypeRegistered = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(EqualException), typeof(IsAssignableFromException))]
    public async Task Send_interface_message_from_anonymous_object()
    {
        var factory = new StubTransportFactory();
        var endpoint = new TransportSendEndpoint(factory, new SendPipe(Pipe.Empty<SendContext>()), new EnvelopeMessageSerializer(), new Uri("loopback://localhost/queue"), new Uri("loopback://localhost/"), new SendContextFactory());

        await endpoint.Send<ICommand>(new { Value = "hello" });

        var msg = Assert.IsAssignableFrom<ICommand>(factory.Transport.Message);
        Assert.Equal("hello", msg.Value);
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(EncoderFallbackException), typeof(InvalidOperationException), typeof(System.Text.Json.JsonException), typeof(IsAssignableFromException))]
    public async Task Respond_interface_message_from_anonymous_object()
    {
        var json = Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"responseAddress\":\"queue:response\",\"message\":{}}");
        var envelope = new EnvelopeMessageContext(json, new Dictionary<string, object>());
        var receiveContext = new ReceiveContextImpl(envelope, null, CancellationToken.None);

        var factory = new StubTransportFactory();

        var ctx = new ConsumeContextImpl<FakeMessage>(receiveContext, factory,
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<PublishContext>()),
            new EnvelopeMessageSerializer(),
            new Uri("loopback://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());

        await ctx.RespondAsync<IReply>(new { Value = "world" });

        var msg = Assert.IsAssignableFrom<IReply>(factory.Transport.Message);
        Assert.Equal("world", msg.Value);
    }

    class FakeMessage { }
}
