namespace MyServiceBus.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Sdk;

public class ConsumeContextTests
{
    [Fact]
    [Throws(typeof(EqualException))]
    public async Task Passes_Message_Through_Pipeline()
    {
        var collected = new List<string>();
        var configurator = new PipeConfigurator<ConsumeContext<string>>();
        configurator.UseExecute(ctx =>
        {
            collected.Add(ctx.Message);
            return Task.CompletedTask;
        });

        var pipe = configurator.Build();
        var context = new DefaultConsumeContext<string>("hello");
        await pipe.Send(context);

        Assert.Equal(new[] { "hello" }, collected);
    }

    [Fact]
    [Throws(typeof(EncoderFallbackException), typeof(JsonException), typeof(UriFormatException))]
    public void ConsumeContext_uses_receive_cancellation_token()
    {
        var cts = new CancellationTokenSource();
        var json = Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{}}");
        var envelope = new EnvelopeMessageContext(json, new Dictionary<string, object>());

        try
        {
            var receiveContext = new ReceiveContextImpl(envelope, null, cts.Token);
            var sut = new ConsumeContextImpl<string>(receiveContext, new StubTransportFactory(),
                new SendPipe(Pipe.Empty<SendContext>()),
                new PublishPipe(Pipe.Empty<PublishContext>()),
                new EnvelopeMessageSerializer(),
                new Uri("rabbitmq://localhost/"),
                new SendContextFactory(),
                new PublishContextFactory());

            Assert.Equal(cts.Token, sut.CancellationToken);
        }
        catch (ObjectDisposedException exc)
        {
            Assert.Fail(exc.Message);
        }
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(EncoderFallbackException), typeof(InvalidOperationException), typeof(JsonException))]
    public async Task Publish_uses_exchange_uri()
    {
        var json = Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{}}");
        var envelope = new EnvelopeMessageContext(json, new Dictionary<string, object>());
        var receiveContext = new ReceiveContextImpl(envelope, null, CancellationToken.None);
        var factory = new CapturingTransportFactory();

        var ctx = new ConsumeContextImpl<FakeMessage>(receiveContext, factory,
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<PublishContext>()),
            new EnvelopeMessageSerializer(),
            new Uri("rabbitmq://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());

        await ctx.PublishAsync(new FakeMessage());

        Assert.Equal(new Uri("rabbitmq://localhost/exchange/MyServiceBus.Tests:FakeMessage"), factory.Address);
        Assert.Equal(new Uri("rabbitmq://localhost/"), factory.Context!.SourceAddress);
        Assert.Equal(new Uri("rabbitmq://localhost/exchange/MyServiceBus.Tests:FakeMessage"), factory.Context!.DestinationAddress);
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(EncoderFallbackException), typeof(JsonException))]
    public async Task Forward_uses_queue_uri()
    {
        var json = Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{}}");
        var envelope = new EnvelopeMessageContext(json, new Dictionary<string, object>());
        var receiveContext = new ReceiveContextImpl(envelope, null, CancellationToken.None);
        var factory = new CapturingTransportFactory();

        var ctx = new ConsumeContextImpl<FakeMessage>(receiveContext, factory,
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<PublishContext>()),
            new EnvelopeMessageSerializer(),
            new Uri("rabbitmq://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());

        await ctx.Forward(new Uri("queue:forward-queue"), new FakeMessage());

        Assert.Equal(new Uri("queue:forward-queue"), factory.Address);
    }

    class FakeMessage { }

    class CapturingTransportFactory : ITransportFactory
    {
        public Uri? Address { get; private set; }
        public SendContext? Context { get; private set; }

        [Throws(typeof(InvalidOperationException))]
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
        {
            Address = address;
            return Task.FromResult<ISendTransport>(new StubSendTransport(this));
        }

        [Throws(typeof(NotImplementedException))]
        public Task<IReceiveTransport> CreateReceiveTransport(
            ReceiveEndpointTopology topology,
            Func<ReceiveContext, Task> handler,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        class StubSendTransport : ISendTransport
        {
            readonly CapturingTransportFactory _factory;

            public StubSendTransport(CapturingTransportFactory factory)
            {
                _factory = factory;
            }

            public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default) where T : class
            {
                _factory.Context = context;
                return Task.CompletedTask;
            }
        }
    }

    class StubTransportFactory : ITransportFactory
    {
        [Throws(typeof(NotImplementedException), typeof(InvalidOperationException))]
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        [Throws(typeof(NotImplementedException))]
        public Task<IReceiveTransport> CreateReceiveTransport(
            ReceiveEndpointTopology topology,
            Func<ReceiveContext, Task> handler,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}

