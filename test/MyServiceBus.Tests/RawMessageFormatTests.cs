using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;

namespace MyServiceBus.Tests;

public class RawMessageFormatTests
{
    class TestMessage
    {
        public string Text { get; set; } = string.Empty;
    }

    class CaptureSendTransport : ISendTransport
    {
        public byte[]? Body { get; private set; }
        public string? ContentType { get; private set; }
        public IDictionary<string, object>? Headers { get; private set; }

        public async Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
            where T : class
        {
            Body = (await context.Serialize(message)).ToArray();
            ContentType = context.Headers.TryGetValue("content_type", out var value) ? value?.ToString() : null;
            Headers = new Dictionary<string, object>(context.Headers);
        }
    }

    class StubTransportFactory : ITransportFactory
    {
        public CaptureSendTransport Transport { get; } = new();

        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => Task.FromResult<ISendTransport>(Transport);

        public Task<IReceiveTransport> CreateReceiveTransport(
            ReceiveEndpointTopology topology,
            Func<ReceiveContext, Task> handler,
            Func<string?, bool>? isMessageTypeRegistered = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    [Fact]
    public async Task Publish_with_raw_serializer_sends_plain_json_payload()
    {
        var factory = new StubTransportFactory();
        var bus = new MessageBus(
            factory,
            new ServiceCollection().BuildServiceProvider(),
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<PublishContext>()),
            new RawJsonMessageSerializer(),
            new Uri("rabbitmq://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());

        await bus.Publish(
            new TestMessage { Text = "hi" },
            ctx => ctx.Headers["NServiceBus.EnclosedMessageTypes"] = "Contracts:TestMessage");

        Assert.Equal("application/json", factory.Transport.ContentType);
        Assert.Contains("\"text\":\"hi\"", Encoding.UTF8.GetString(factory.Transport.Body!));
        Assert.Equal(
            "Contracts:TestMessage",
            factory.Transport.Headers!["NServiceBus.EnclosedMessageTypes"]);
    }

    [Fact]
    public async Task Send_with_raw_serializer_sends_plain_json_payload()
    {
        var factory = new StubTransportFactory();
        var bus = new MessageBus(
            factory,
            new ServiceCollection().BuildServiceProvider(),
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<PublishContext>()),
            new RawJsonMessageSerializer(),
            new Uri("rabbitmq://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());

        var endpoint = await bus.GetSendEndpoint(new Uri("queue:test"));
        await endpoint.Send(
            new TestMessage { Text = "hi" },
            ctx => ctx.Headers["NServiceBus.EnclosedMessageTypes"] = "Contracts:TestMessage");

        Assert.Equal("application/json", factory.Transport.ContentType);
        Assert.Contains("\"text\":\"hi\"", Encoding.UTF8.GetString(factory.Transport.Body!));
        Assert.Equal(
            "Contracts:TestMessage",
            factory.Transport.Headers!["NServiceBus.EnclosedMessageTypes"]);
    }
}
