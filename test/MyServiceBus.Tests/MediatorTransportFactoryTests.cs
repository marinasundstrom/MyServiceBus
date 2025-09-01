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

    [Fact]
    [Throws(typeof(TrueException), typeof(Exception))]
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

        var receive = await factory.CreateReceiveTransport(topology, ctx =>
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
}
