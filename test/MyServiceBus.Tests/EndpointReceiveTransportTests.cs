using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus.Serialization;
using MyServiceBus.Transports;
using Shouldly;
using Xunit;

namespace MyServiceBus.Tests;

public class EndpointReceiveTransportTests
{
    class StubEndpoint : IEndpoint
    {
        public EndpointCapabilities Capabilities => EndpointCapabilities.None;

        public Task Send<T>(T message, Action<ISendContext>? configure = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public async IAsyncEnumerable<ReceiveContext> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var envelope = new Envelope<string>
            {
                MessageId = Guid.Empty,
                MessageType = new(),
                Message = "hi"
            };
            var payload = JsonSerializer.SerializeToUtf8Bytes(envelope, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var headers = new Dictionary<string, object> { ["content_type"] = "application/vnd.masstransit+json" };
            var msgContext = new MessageContextFactory().CreateMessageContext(new TestTransportMessage(headers, payload));
            yield return new ReceiveContextImpl(msgContext, null, cancellationToken);
        }

        public IDisposable Subscribe(Func<ReceiveContext, Task> handler) => throw new NotSupportedException();
    }

    class TestTransportMessage : ITransportMessage
    {
        public TestTransportMessage(IDictionary<string, object> headers, byte[] payload)
        {
            Headers = headers;
            Payload = payload;
        }

        public IDictionary<string, object> Headers { get; }
        public bool IsDurable => false;
        public byte[] Payload { get; }
    }

    [Fact]
    public async Task Pump_invokes_handler()
    {
        var endpoint = new StubEndpoint();
        ReceiveContext? received = null;
        var transport = new EndpointReceiveTransport(endpoint, ctx =>
        {
            received = ctx;
            return Task.CompletedTask;
        });
        await transport.Start();
        await Task.Delay(50);
        await transport.Stop();

        received.ShouldNotBeNull();
        received!.TryGetMessage<string>(out var msg).ShouldBeTrue();
        msg.ShouldBe("hi");
    }
}
