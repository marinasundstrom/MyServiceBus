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
        var receiveContext = new ReceiveContextImpl(envelope, cts.Token);
        var sut = new ConsumeContextImpl<string>(receiveContext, new StubTransportFactory());

        Assert.Equal(cts.Token, sut.CancellationToken);
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

