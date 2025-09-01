using System.Text.Json;
using System.Linq;
using MyServiceBus;
using MyServiceBus.Serialization;
using Xunit;
using Xunit.Sdk;

namespace MyServiceBus.Tests;

public class BatchHandlingTests
{
    private class SampleMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    [Fact]
    [Throws(typeof(ContainsException), typeof(TrueException), typeof(NotNullException), typeof(EqualException), typeof(Exception))]
    public async Task Envelope_serializer_handles_batch_message()
    {
        var batch = new Batch<SampleMessage>(
            new SampleMessage { Value = "A" },
            new SampleMessage { Value = "B" }
        );

        var serializer = new EnvelopeMessageSerializer();
        var context = new MessageSerializationContext<Batch<SampleMessage>>(batch)
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = null,
            MessageType = [
                NamingConventions.GetMessageUrn(typeof(Batch<SampleMessage>)),
                NamingConventions.GetMessageUrn(typeof(SampleMessage))
            ],
            Headers = new Dictionary<string, object>(),
            SentTime = DateTimeOffset.UtcNow,
            HostInfo = new HostInfo
            {
                MachineName = Environment.MachineName,
                ProcessName = Environment.ProcessPath ?? "unknown",
                ProcessId = Environment.ProcessId,
                Assembly = typeof(SampleMessage).Assembly.GetName().Name ?? "unknown",
                AssemblyVersion = typeof(SampleMessage).Assembly.GetName().Version?.ToString() ?? "unknown",
                FrameworkVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                MassTransitVersion = "test",
                OperatingSystemVersion = Environment.OSVersion.VersionString,
            },
        };

        var bytes = await serializer.SerializeAsync(context);
        using var doc = JsonDocument.Parse(bytes);
        var messageElement = doc.RootElement.GetProperty("message");
        Assert.Equal(JsonValueKind.Array, messageElement.ValueKind);
        Assert.Equal(2, messageElement.GetArrayLength());

        var typeElement = doc.RootElement.GetProperty("messageType");
        Assert.Equal(2, typeElement.GetArrayLength());
        var types = typeElement.EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains(NamingConventions.GetMessageUrn(typeof(Batch<SampleMessage>)), types);
        Assert.Contains(NamingConventions.GetMessageUrn(typeof(SampleMessage)), types);

        var envelopeContext = new EnvelopeMessageContext(bytes, new Dictionary<string, object>());

        Assert.True(envelopeContext.TryGetMessage<Batch<SampleMessage>>(out var deserialized));
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.Count);
        Assert.Equal("A", deserialized[0].Value);
        Assert.Equal("B", deserialized[1].Value);
    }
}
