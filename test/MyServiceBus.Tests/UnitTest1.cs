namespace MyServiceBus.Tests;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using MyServiceBus.Serialization;
using Xunit;

public class EnvelopeMessageContextTests
{
    public class SampleMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    [Fact]
    public void Can_Parse_Metadata_And_Deserialize_Message()
    {
        // Arrange: skapa test-envelope som JSON
        var envelope = new
        {
            messageId = Guid.NewGuid(),
            correlationId = Guid.NewGuid(),
            messageType = new[] { "urn:message:SampleMessage" },
            headers = new Dictionary<string, object>
            {
                { "CustomHeader", "123" }
            },
            message = new SampleMessage
            {
                Value = "Hello world"
            }
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(envelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var context = new EnvelopeMessageContext(json, new Dictionary<string, object>());

        // Act + Assert
        Assert.NotEqual(Guid.Empty, context.MessageId);
        Assert.NotEmpty(context.MessageType);
        Assert.True(context.Headers.ContainsKey("CustomHeader"));

        Assert.True(context.TryGetMessage<SampleMessage>(out var message));
        Assert.NotNull(message);
        Assert.Equal("Hello world", message!.Value);
    }
}
