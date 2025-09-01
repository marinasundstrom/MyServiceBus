using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Topology;
using Xunit;
using Xunit.Sdk;

public class FaultHandlingTests
{
    class TestMessage
    {
        public string Text { get; set; } = string.Empty;
    }

    class FaultingConsumer : IConsumer<TestMessage>
    {
        public Task Consume(ConsumeContext<TestMessage> context) => throw new InvalidOperationException("boom");
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(Exception))]
    public async Task Sends_fault_when_consumer_throws()
    {
        var services = new ServiceCollection();
        services.AddTransient<FaultingConsumer>();
        var provider = services.BuildServiceProvider();

        var transportFactory = new CaptureTransportFactory();

        var receiveContext = new StubReceiveContext
        {
            Message = new TestMessage { Text = "hi" },
            MessageId = Guid.NewGuid(),
            ResponseAddress = new Uri("rabbitmq://localhost/response")
        };

        var context = new ConsumeContextImpl<TestMessage>(receiveContext, transportFactory);
        var filter = new ConsumerMessageFilter<FaultingConsumer, TestMessage>(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => filter.Send(context, Pipe.Empty<ConsumeContext<TestMessage>>()));

        var fault = Assert.IsType<Fault<TestMessage>>(transportFactory.SendTransport.Sent);
        Assert.Equal("boom", fault.Exceptions[0].Message);
    }

    class CaptureSendTransport : ISendTransport
    {
        public object? Sent { get; private set; }

        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default) where T : class
        {
            Sent = message;
            return Task.CompletedTask;
        }
    }

    class CaptureTransportFactory : ITransportFactory
    {
        public readonly CaptureSendTransport SendTransport = new();

        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => Task.FromResult<ISendTransport>(SendTransport);

        public Task<IReceiveTransport> CreateReceiveTransport(ReceiveEndpointTopology topology, Func<ReceiveContext, Task> handler, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    class StubReceiveContext : ReceiveContext
    {
        public TestMessage Message { get; set; } = new();
        public Guid MessageId { get; set; }
        public IList<string> MessageType { get; set; } = [NamingConventions.GetMessageUrn(typeof(TestMessage))];
        public Uri? ResponseAddress { get; set; }
        public Uri? FaultAddress { get; set; }
        public IDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
        public CancellationToken CancellationToken => CancellationToken.None;
        public bool TryGetMessage<T>(out T? message) where T : class
        {
            if (typeof(T) == typeof(TestMessage))
            {
                message = (T)(object)Message;
                return true;
            }

            message = null;
            return false;
        }
    }
}
