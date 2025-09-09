using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyServiceBus;
using Xunit;

public class DefaultLoggerTests
{
    [Fact]
    public void AddServiceBus_adds_console_logger_when_missing()
    {
        var services = new ServiceCollection();
        services.AddServiceBus(cfg => cfg.UsingMediator());

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetService<ILoggerFactory>();

        Assert.NotNull(factory);
    }
}
