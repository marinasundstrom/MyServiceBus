using MyServiceBus;
using Xunit;
using Xunit.Sdk;

public class BusFactoryTests
{
    [Fact]
    [Throws(typeof(NotNullException))]
    public void Factory_creates_bus()
    {
        var bus = MyServiceBus.MessageBus.Factory.CreateUsingRabbitMq(cfg => cfg.Host("localhost"));
        Assert.NotNull(bus);
    }
}
