using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MyServiceBus.Serialization;
using Xunit.Sdk;
using Xunit;

namespace MyServiceBus.Tests;

public class RawJsonMessageSerializerTests
{
    class TestMessage
    {
        public string Text { get; set; } = string.Empty;
    }

    [Fact]
    [Throws(typeof(System.NotSupportedException), typeof(DecoderFallbackException), typeof(ContainsException), typeof(KeyNotFoundException))]
    public async Task Serializes_message_as_json()
    {
        var serializer = new RawJsonMessageSerializer();
        var message = new TestMessage { Text = "hi" };
        var headers = new Dictionary<string, object>();
        var context = new MessageSerializationContext<TestMessage>(message)
        {
            Headers = headers
        };

        var bytes = await serializer.SerializeAsync(context);
        var json = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"text\":\"hi\"", json);
        Assert.Equal("application/json", headers["content_type"]);
    }
}
