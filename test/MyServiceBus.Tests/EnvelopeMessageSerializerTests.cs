namespace MyServiceBus.Tests;

using System.Collections.Generic;
using MyServiceBus.Serialization;
using Xunit;

public class EnvelopeMessageSerializerTests
{
    public class SampleMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    [Fact]
    [Throws(typeof(Exception))]
    public async Task Foo2()
    {
        var foo = new SampleMessage
        {
            Value = "Test"
        };

        var serializer = new EnvelopeMessageSerializer();
        var context = new MessageSerializationContext<SampleMessage>(foo)
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = null,
            MessageType = [NamingConventions.GetMessageUrn(typeof(SampleMessage))],
            Headers = new Dictionary<string, object>(),
            SentTime = DateTimeOffset.Now,
            HostInfo = new HostInfo
            {
                MachineName = Environment.MachineName,
                ProcessName = Environment.ProcessPath ?? "unknown",
                ProcessId = Environment.ProcessId,
                Assembly = typeof(SampleMessage).Assembly.GetName().Name ?? "unknown",
                AssemblyVersion = typeof(SampleMessage).Assembly.GetName().Version?.ToString() ?? "unknown",
                FrameworkVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                MassTransitVersion = "your-custom-version", // replace as needed
                OperatingSystemVersion = Environment.OSVersion.VersionString
            },
        };
        var r = await serializer.SerializeAsync(context);
    }
}