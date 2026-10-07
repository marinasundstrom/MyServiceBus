using MyServiceBus.Serialization;
using System.Text;

namespace MyServiceBus.Tests;

public class InterfacePayloadTests
{
    public interface IOrder { int Quantity { get; } }
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{bad-json")]
    public void Invalid_envelope_is_a_deserialization_failure(string json)
        => Assert.Throws<MessageDeserializationException>(() => new EnvelopeMessageContext(
            Encoding.UTF8.GetBytes(json), new Dictionary<string, object>()));

    [Fact]
    public void Interface_payload_is_materialized_and_validated_before_consumption()
    {
        var context = new EnvelopeMessageContext(Encoding.UTF8.GetBytes("{\"message\":{\"quantity\":3}}"), new Dictionary<string, object>());
        Assert.True(context.TryGetMessage<IOrder>(out var order));
        Assert.Equal(3, order!.Quantity);
        var invalid = new EnvelopeMessageContext(Encoding.UTF8.GetBytes("{\"message\":{\"quantity\":\"bad\"}}"), new Dictionary<string, object>());
        Assert.Throws<MessageDeserializationException>(() => invalid.TryGetMessage<IOrder>(out _));
    }

}
