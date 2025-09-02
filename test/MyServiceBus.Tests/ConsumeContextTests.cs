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
    [Throws(typeof(EncoderFallbackException))]
    public void ConsumeContext_uses_receive_cancellation_token()
    {
        var cts = new CancellationTokenSource();
        var json = Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{}}");
        var envelope = new EnvelopeMessageContext(json, new Dictionary<string, object>());

        try
        {
            var receiveContext = new ReceiveContextImpl(envelope, cts.Token);
            var sut = new ConsumeContextImpl<string>(receiveContext, new StubTransportFactory(),
                new SendPipe(Pipe.Empty<SendContext>()),
                new PublishPipe(Pipe.Empty<SendContext>()),
                new EnvelopeMessageSerializer());

            Assert.Equal(cts.Token, sut.CancellationToken);
        }
        catch (ObjectDisposedException exc)
        {
            Assert.Fail(exc.Message);
        }
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(EncoderFallbackException), typeof(InvalidOperationException))]
    public async Task Publish_uses_exchange_uri()
    {
        var json = Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{}}");
        var envelope = new EnvelopeMessageContext(json, new Dictionary<string, object>());
        var receiveContext = new ReceiveContextImpl(envelope, CancellationToken.None);
        var factory = new CapturingTransportFactory();

        var ctx = new ConsumeContextImpl<FakeMessage>(receiveContext, factory,
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<SendContext>()),
            new EnvelopeMessageSerializer());

        await ctx.PublishAsync(new FakeMessage());

        Assert.Equal(new Uri("rabbitmq://localhost/exchange/MyServiceBus.Tests:FakeMessage"), factory.Address);
    }

    class FakeMessage { }

    class CapturingTransportFactory : ITransportFactory
    {
        public Uri? Address { get; private set; }

        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
        {
            Address = address;
            return Task.FromResult<ISendTransport>(new StubSendTransport());
        }

        [Throws(typeof(NotImplementedException))]
        public Task<IReceiveTransport> CreateReceiveTransport(
            ReceiveEndpointTopology topology,
            Func<ReceiveContext, Task> handler,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        class StubSendTransport : ISendTransport
        {
            public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default) where T : class
                => Task.CompletedTask;
        }
    }

    class StubTransportFactory : ITransportFactory
    {
        [Throws(typeof(NotImplementedException))]
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

