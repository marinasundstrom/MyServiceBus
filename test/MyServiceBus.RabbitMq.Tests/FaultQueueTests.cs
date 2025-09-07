using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Serialization;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using MyServiceBus.Topology;

namespace MyServiceBus.RabbitMq.Tests;

public class FaultQueueTests
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
    [Throws(typeof(UriFormatException), typeof(JsonException), typeof(ArgumentException), typeof(KeyNotFoundException))]
    public async Task Sends_fault_to_fault_queue_when_consumer_faults()
    {
        var services = new ServiceCollection();
        services.AddTransient<FaultingConsumer>();
        var provider = services.BuildServiceProvider();

        var errorUri = new Uri("rabbitmq://localhost/exchange/test-queue_error");
        var faultUri = new Uri("rabbitmq://localhost/exchange/test-queue_fault");
        var json = Encoding.UTF8.GetBytes($"{{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{{\"text\":\"hi\"}}}}");
        var headers = new Dictionary<string, object> { [MessageHeaders.FaultAddress] = faultUri.ToString() };
        var envelope = new EnvelopeMessageContext(json, headers);
        var receiveContext = new ReceiveContextImpl(envelope, errorUri);

        var transportFactory = new MultiCaptureTransportFactory();
        var context = new ConsumeContextImpl<TestMessage>(receiveContext, transportFactory,
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<PublishContext>()),
            new EnvelopeMessageSerializer(),
            new Uri("rabbitmq://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());

        var configurator = new PipeConfigurator<ConsumeContext<TestMessage>>();
        configurator.UseFilter(new ErrorTransportFilter<TestMessage>());
        configurator.UseFilter(new ConsumerFaultFilter<FaultingConsumer, TestMessage>(provider));
        configurator.UseFilter(new ConsumerMessageFilter<FaultingConsumer, TestMessage>(provider));
        var pipe = new ConsumePipe<TestMessage>(configurator.Build());

        await Should.ThrowAsync<InvalidOperationException>(() => pipe.Send(context));

        transportFactory.Transports[faultUri].Sent.ShouldBeOfType<Fault<TestMessage>>();
        transportFactory.Transports[errorUri].Sent.ShouldBeOfType<TestMessage>();
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(JsonException), typeof(ArgumentException), typeof(KeyNotFoundException))]
    public async Task Sends_fault_to_fault_queue_when_handler_faults()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var errorUri = new Uri("rabbitmq://localhost/exchange/test-queue_error");
        var faultUri = new Uri("rabbitmq://localhost/exchange/test-queue_fault");
        var json = Encoding.UTF8.GetBytes($"{{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{{\"text\":\"hi\"}}}}");
        var headers = new Dictionary<string, object> { [MessageHeaders.FaultAddress] = faultUri.ToString() };
        var envelope = new EnvelopeMessageContext(json, headers);
        var receiveContext = new ReceiveContextImpl(envelope, errorUri);

        var transportFactory = new MultiCaptureTransportFactory();
        var context = new ConsumeContextImpl<TestMessage>(receiveContext, transportFactory,
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<PublishContext>()),
            new EnvelopeMessageSerializer(),
            new Uri("rabbitmq://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());

        var configurator = new PipeConfigurator<ConsumeContext<TestMessage>>();
        configurator.UseFilter(new ErrorTransportFilter<TestMessage>());
        var faultFilterType = typeof(MessageBus).Assembly.GetType("MyServiceBus.HandlerFaultFilter`1")!.MakeGenericType(typeof(TestMessage));
        var faultFilter = (IFilter<ConsumeContext<TestMessage>>)Activator.CreateInstance(faultFilterType, provider)!;
        configurator.UseFilter(faultFilter);
        var handlerFilterType = typeof(MessageBus).Assembly.GetType("MyServiceBus.HandlerMessageFilter`1")!.MakeGenericType(typeof(TestMessage));
        Func<ConsumeContext<TestMessage>, Task> handler = _ => throw new InvalidOperationException("boom");
        var handlerFilter = (IFilter<ConsumeContext<TestMessage>>)Activator.CreateInstance(handlerFilterType, handler)!;
        configurator.UseFilter(handlerFilter);
        var pipe = new ConsumePipe<TestMessage>(configurator.Build());

        await Should.ThrowAsync<InvalidOperationException>(() => pipe.Send(context));

        transportFactory.Transports[faultUri].Sent.ShouldBeOfType<Fault<TestMessage>>();
        transportFactory.Transports[errorUri].Sent.ShouldBeOfType<TestMessage>();
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

    class MultiCaptureTransportFactory : ITransportFactory
    {
        public readonly Dictionary<Uri, CaptureSendTransport> Transports = new();

        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
        {
            if (!Transports.TryGetValue(address, out var transport))
            {
                transport = new CaptureSendTransport();
                Transports[address] = transport;
            }
            return Task.FromResult<ISendTransport>(transport);
        }

        [Throws(typeof(NotImplementedException))]
        public Task<IReceiveTransport> CreateReceiveTransport(ReceiveEndpointTopology topology, Func<ReceiveContext, Task> handler, Func<string?, bool>? isMessageTypeRegistered = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
