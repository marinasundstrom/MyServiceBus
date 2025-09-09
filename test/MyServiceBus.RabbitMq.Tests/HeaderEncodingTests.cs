using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RabbitMQ.Client;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus;
using MyServiceBus.Serialization;

namespace MyServiceBus.RabbitMq.Tests;

public class HeaderEncodingTests
{
    class TestMessage { public string Text { get; set; } = string.Empty; }

    class FaultingConsumer : IConsumer<TestMessage>
    {
        [Throws(typeof(InvalidOperationException))]
        public Task Consume(ConsumeContext<TestMessage> context) => throw new InvalidOperationException("boom");
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(ArgumentException), typeof(InvalidOperationException), typeof(JsonException))]
    public async Task Faulted_message_headers_include_mt_prefix()
    {
        var channel = Substitute.For<IChannel>();
        BasicProperties? captured = null;
        channel.BasicPublishAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask)
            .AndDoes(ci => captured = ci.Arg<BasicProperties>());

        var services = new ServiceCollection();
        services.AddTransient<FaultingConsumer>();
        var provider = services.BuildServiceProvider();

        var errorUri = new Uri("rabbitmq://localhost/exchange/test-queue_error");
        var json = Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{\"text\":\"hi\"}}");
        var envelope = new EnvelopeMessageContext(json, new Dictionary<string, object>());
        var receiveContext = new ReceiveContextImpl(envelope, errorUri);

        var transportFactory = Substitute.For<ITransportFactory>();
        transportFactory.GetSendTransport(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<ISendTransport>(new RabbitMqQueueSendTransport(channel, "test-queue_error")));

        var consumeContext = new ConsumeContextImpl<TestMessage>(receiveContext, transportFactory,
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<PublishContext>()),
            new EnvelopeMessageSerializer(),
            new Uri("rabbitmq://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());

        var configurator = new PipeConfigurator<ConsumeContext<TestMessage>>();
        configurator.UseFilter(new ErrorTransportFilter<TestMessage>());
        var factory = new ScopeConsumerFactory<FaultingConsumer>(provider);
        configurator.UseFilter(new ConsumerMessageFilter<FaultingConsumer, TestMessage>(factory));
        var pipe = new ConsumePipe<TestMessage>(configurator.Build());

        await Should.ThrowAsync<InvalidOperationException>([Throws(typeof(InvalidCastException))] () => pipe.Send(consumeContext));

        captured.ShouldNotBeNull();
        captured!.Headers.ShouldContainKey("MT-Host-MachineName");
    }
}
