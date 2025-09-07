using MyServiceBus.Serialization;
using NSubstitute;
using RabbitMQ.Client;
using Xunit;

namespace MyServiceBus.RabbitMq.Tests;

public class RabbitMqReceiveContextTests
{
    [Fact]
    public void Captures_rabbitmq_metadata()
    {
        var msgContext = Substitute.For<IMessageContext>();
        var props = new BasicProperties();
        var ctx = new RabbitMqReceiveContext(msgContext, props, 10, "ex", "rk");

        Assert.Equal(props, ctx.Properties);
        Assert.Equal((ulong)10, ctx.DeliveryTag);
        Assert.Equal("ex", ctx.Exchange);
        Assert.Equal("rk", ctx.RoutingKey);
    }
}
