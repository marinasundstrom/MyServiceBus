using MyServiceBus;
using Xunit;

public class BusFactoryTests
{
    [Fact]
    public void Factory_creates_bus()
    {
        var bus = MessageBus.Factory.CreateUsingRabbitMq(cfg => cfg.Host("localhost"));
        Assert.NotNull(bus);
    }
}
