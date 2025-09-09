using MyServiceBus;
using Xunit;
using Xunit.Sdk;

public class BusFactoryTests
{
    [Fact]
    public void Factory_creates_bus()
    {
        var bus = MessageBus.Factory.Create<RabbitMqFactoryConfigurator>(cfg => cfg.Host("localhost"));
        Assert.NotNull(bus);
    }
}
