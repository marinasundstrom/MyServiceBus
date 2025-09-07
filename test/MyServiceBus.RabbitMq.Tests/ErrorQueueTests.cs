using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus.RabbitMq.Tests;

public class ErrorQueueTests
{
    class TestMessage
    {
        public string Text { get; set; } = string.Empty;
    }

    class FaultingConsumer : IConsumer<TestMessage>
    {
        [Throws(typeof(InvalidOperationException))]
        public Task Consume(ConsumeContext<TestMessage> context)
            => throw new InvalidOperationException("boom");
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(JsonException), typeof(ArgumentException), typeof(KeyNotFoundException))]
    public async Task Moves_message_to_error_queue_when_consumer_faults()
    {
        var services = new ServiceCollection();
        services.AddTransient<FaultingConsumer>();
        var provider = services.BuildServiceProvider();

        var errorUri = new Uri("rabbitmq://localhost/exchange/test-queue_error");
        var faultUri = new Uri("rabbitmq://localhost/exchange/custom_fault");
        var json = Encoding.UTF8.GetBytes($"{{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{{\"text\":\"hi\"}}}}");
        var headers = new Dictionary<string, object> { [MessageHeaders.FaultAddress] = faultUri.ToString() };
        var envelope = new EnvelopeMessageContext(json, headers);
        var receiveContext = new ReceiveContextImpl(envelope, errorUri);

        var transportFactory = new CaptureTransportFactory();
        var context = new ConsumeContextImpl<TestMessage>(receiveContext, transportFactory,
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<PublishContext>()),
            new EnvelopeMessageSerializer(),
            new Uri("rabbitmq://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());

        var configurator = new PipeConfigurator<ConsumeContext<TestMessage>>();
        configurator.UseFilter(new ErrorTransportFilter<TestMessage>());
        configurator.UseFilter(new ConsumerMessageFilter<FaultingConsumer, TestMessage>(provider));
        var pipe = new ConsumePipe<TestMessage>(configurator.Build());

        await Should.ThrowAsync<InvalidOperationException>([Throws(typeof(InvalidCastException))] () => pipe.Send(context));

        transportFactory.Address.ShouldBe(errorUri);
        var sent = transportFactory.SendTransport.Sent.ShouldBeOfType<TestMessage>();
        sent.Text.ShouldBe("hi");

        var errorHeaders = transportFactory.SendTransport.Context!.Headers;
        errorHeaders[MessageHeaders.ExceptionType].ShouldBe(typeof(InvalidOperationException).FullName);
        errorHeaders[MessageHeaders.Reason].ShouldBe("fault");
        errorHeaders[MessageHeaders.RedeliveryCount].ShouldBe(0);
        errorHeaders.ShouldContainKey(MessageHeaders.HostMachineName);
        errorHeaders.ShouldContainKey(MessageHeaders.HostProcessName);
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(JsonException), typeof(ArgumentException), typeof(KeyNotFoundException))]
    public async Task Sanitizes_malformed_redelivery_count_header()
    {
        var services = new ServiceCollection();
        services.AddTransient<FaultingConsumer>();
        var provider = services.BuildServiceProvider();

        var errorUri = new Uri("rabbitmq://localhost/exchange/test-queue_error");
        var faultUri = new Uri("rabbitmq://localhost/exchange/custom_fault");
        var json = Encoding.UTF8.GetBytes($"{{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{{\"text\":\"hi\"}}}}");
        var headers = new Dictionary<string, object>
        {
            [MessageHeaders.FaultAddress] = faultUri.ToString(),
            [MessageHeaders.RedeliveryCount] = "not-a-number"
        };
        var envelope = new EnvelopeMessageContext(json, headers);
        var receiveContext = new ReceiveContextImpl(envelope, errorUri);

        var transportFactory = new CaptureTransportFactory();
        var context = new ConsumeContextImpl<TestMessage>(receiveContext, transportFactory,
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<PublishContext>()),
            new EnvelopeMessageSerializer(),
            new Uri("rabbitmq://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());

        var configurator = new PipeConfigurator<ConsumeContext<TestMessage>>();
        configurator.UseFilter(new ErrorTransportFilter<TestMessage>());
        configurator.UseFilter(new ConsumerMessageFilter<FaultingConsumer, TestMessage>(provider));
        var pipe = new ConsumePipe<TestMessage>(configurator.Build());

        await Should.ThrowAsync<InvalidOperationException>(() => pipe.Send(context));

        transportFactory.Address.ShouldBe(errorUri);
        var errorHeaders = transportFactory.SendTransport.Context!.Headers;
        errorHeaders[MessageHeaders.RedeliveryCount].ShouldBe(0);
    }

    class CaptureSendTransport : ISendTransport
    {
        public object? Sent { get; private set; }
        public SendContext? Context { get; private set; }

        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default) where T : class
        {
            Sent = message;
            Context = context;
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
        public Task<IReceiveTransport> CreateReceiveTransport(ReceiveEndpointTopology topology, Func<ReceiveContext, Task> handler, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
