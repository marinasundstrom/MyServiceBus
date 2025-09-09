using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Shouldly;
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
        [Throws(typeof(InvalidOperationException))]
        public Task Consume(ConsumeContext<TestMessage> context) => throw new InvalidOperationException("boom");
    }

    [Fact]
    [Throws(typeof(Exception))]
    public async Task Sends_fault_to_fault_address_when_consumer_throws()
    {
        var services = new ServiceCollection();
        services.AddTransient<FaultingConsumer>();
        var provider = services.BuildServiceProvider();

        var transportFactory = new CaptureTransportFactory();

        var faultUri = new Uri("rabbitmq://localhost/fault");
        var receiveContext = new StubReceiveContext
        {
            Message = new TestMessage { Text = "hi" },
            MessageId = Guid.NewGuid(),
            FaultAddress = faultUri
        };

        var context = new ConsumeContextImpl<TestMessage>(receiveContext, transportFactory,
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<PublishContext>()),
            new EnvelopeMessageSerializer(),
            new Uri("rabbitmq://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());

        var configurator = new PipeConfigurator<ConsumeContext<TestMessage>>();
        configurator.UseFilter(new ConsumerFaultFilter<FaultingConsumer, TestMessage>(provider));
        configurator.UseRetry(1);
        var factory = new ScopeConsumerFactory<FaultingConsumer>(provider);
        configurator.UseFilter(new ConsumerMessageFilter<FaultingConsumer, TestMessage>(factory));
        var pipe = new ConsumePipe<TestMessage>(configurator.Build());

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipe.Send(context));

        transportFactory.Address.ShouldBe(faultUri);
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
        public Uri? Address { get; private set; }

        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
        {
            Address = address;
            return Task.FromResult<ISendTransport>(SendTransport);
        }

        [Throws(typeof(NotImplementedException))]
        public Task<IReceiveTransport> CreateReceiveTransport(ReceiveEndpointTopology topology, Func<ReceiveContext, Task> handler, Func<string?, bool>? isMessageTypeRegistered = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    class StubReceiveContext : ReceiveContext
    {
        public TestMessage Message { get; set; } = new();
        public Guid MessageId { get; set; }
        public IList<string> MessageType { get; set; } = [MessageUrn.For(typeof(TestMessage))];
        public Uri? ResponseAddress { get; set; }
        public Uri? FaultAddress { get; set; }
        public Uri? ErrorAddress { get; set; }
        public IDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
        public CancellationToken CancellationToken => CancellationToken.None;
        [Throws(typeof(InvalidCastException))]
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
