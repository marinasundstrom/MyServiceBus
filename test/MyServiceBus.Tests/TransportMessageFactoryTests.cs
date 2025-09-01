using System.Text;
using MyServiceBus.Serialization;
using MyServiceBus.Transports;
using Xunit;
using Xunit.Sdk;

namespace MyServiceBus.Tests;

public class TransportMessageFactoryTests
{
    private class StubTransportMessage : ITransportMessage
    {
        public IDictionary<string, object> Headers { get; init; } = new Dictionary<string, object>();
        public bool IsDurable { get; init; }
        public byte[] Payload { get; init; } = Array.Empty<byte>();
    }

    [Fact]
    [Throws(typeof(IsTypeException), typeof(Exception))]
    public void CreateMessageContext_EnvelopeContentType_ReturnsEnvelopeContext()
    {
        var payload = Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{}}");
        var headers = new Dictionary<string, object>
        {
            {"content_type", "application/vnd.mybus.envelope+json"}
        };
        ITransportMessage transport = new StubTransportMessage { Headers = headers, Payload = payload };
        var factory = new MessageContextFactory();
        var ctx = factory.CreateMessageContext(transport);
        Assert.IsType<EnvelopeMessageContext>(ctx);
    }

    [Fact]
    [Throws(typeof(IsTypeException), typeof(Exception))]
    public void CreateMessageContext_MassTransitContentType_ReturnsEnvelopeContext()
    {
        var payload = Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{}}");
        var headers = new Dictionary<string, object>
        {
            {"content_type", "application/vnd.masstransit+json"}
        };
        ITransportMessage transport = new StubTransportMessage { Headers = headers, Payload = payload };
        var factory = new MessageContextFactory();
        var ctx = factory.CreateMessageContext(transport);
        Assert.IsType<EnvelopeMessageContext>(ctx);
    }

    [Fact]
    [Throws(typeof(IsTypeException), typeof(Exception))]
    public void CreateMessageContext_NoContentType_ReturnsEnvelopeContext()
    {
        var payload = Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{}}");
        var headers = new Dictionary<string, object>();
        ITransportMessage transport = new StubTransportMessage { Headers = headers, Payload = payload };
        var factory = new MessageContextFactory();
        var ctx = factory.CreateMessageContext(transport);
        Assert.IsType<EnvelopeMessageContext>(ctx);
    }

    [Fact]
    [Throws(typeof(TrueException), typeof(Exception))]
    public void EnvelopeMessageContext_MergesTransportHeaders()
    {
        var payload = Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"headers\":{\"Custom\":\"123\"},\"message\":{}}");
        var headers = new Dictionary<string, object>
        {
            {"content_type", "application/vnd.mybus.envelope+json"},
            {"Transport", "456"}
        };
        ITransportMessage transport = new StubTransportMessage { Headers = headers, Payload = payload };
        var factory = new MessageContextFactory();
        var ctx = factory.CreateMessageContext(transport);
        Assert.True(ctx.Headers.ContainsKey("Custom"));
        Assert.True(ctx.Headers.ContainsKey("Transport"));
    }
}
