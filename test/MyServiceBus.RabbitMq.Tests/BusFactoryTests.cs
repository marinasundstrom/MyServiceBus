using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyServiceBus;
using Xunit;
using Xunit.Sdk;

public class BusFactoryTests
{
    [Fact]
    public void Factory_creates_bus()
    {
        var bus = MyServiceBus.MessageBus.Factory.Create<RabbitMqFactoryConfigurator>(cfg => cfg.Host("localhost"));
        Assert.NotNull(bus);
    }

    [Fact]
    public void Factory_configures_console_logger()
    {
        var cfg = new RabbitMqFactoryConfigurator();
        cfg.Host("localhost");

        var services = new ServiceCollection();
        cfg.Configure(services);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetService<ILoggerFactory>();

        Assert.NotNull(factory);
    }
}
