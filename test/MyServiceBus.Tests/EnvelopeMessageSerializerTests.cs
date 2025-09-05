namespace MyServiceBus.Tests;

using System;
using System.Text.Json;
using MyServiceBus.Serialization;
using MyServiceBus;
using Xunit;

public class EnvelopeMessageSerializerTests
{
    public class SampleMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    [Fact]
    [Throws(typeof(Exception))]
    public async Task Envelope_contains_addresses()
    {
        var message = new SampleMessage
        {
            Value = "Test"
        };

        var serializer = new EnvelopeMessageSerializer();
        var sendContext = new SendContext([typeof(SampleMessage)], serializer);

        var bytes = await sendContext.Serialize(message);
        var envelope = System.Text.Json.JsonSerializer.Deserialize<Envelope<SampleMessage>>(bytes.Span);

        Assert.NotNull(envelope);
        Assert.Equal(new Uri("loopback://localhost/source"), envelope!.SourceAddress);
        Assert.Equal(new Uri($"loopback://localhost/{nameof(SampleMessage)}"), envelope.DestinationAddress);
    }

    [Fact]
    [Throws(typeof(Exception))]
    public async Task Envelope_omits_mt_host_headers()
    {
        var message = new SampleMessage { Value = "Test" };

        var serializer = new EnvelopeMessageSerializer();
        var sendContext = new SendContext([typeof(SampleMessage)], serializer);
        sendContext.Headers[MessageHeaders.HostMachineName] = "machine";
        sendContext.Headers[MessageHeaders.HostProcessName] = "proc";

        var bytes = await sendContext.Serialize(message);
        var envelope = JsonSerializer.Deserialize<Envelope<SampleMessage>>(bytes.Span);

        Assert.NotNull(envelope);
        Assert.False(envelope!.Headers.ContainsKey(MessageHeaders.HostMachineName));
        Assert.False(envelope.Headers.ContainsKey(MessageHeaders.HostProcessName));
    }
}
