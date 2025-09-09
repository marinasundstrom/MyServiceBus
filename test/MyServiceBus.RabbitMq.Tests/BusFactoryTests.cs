using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyServiceBus;
using Xunit;

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

    [Fact]
    public void Builder_applies_logger_and_services()
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

        MyServiceBus.MessageBus.Factory
            .WithLoggerFactory(loggerFactory)
            .ConfigureServices(s => s.AddSingleton("hello"))
            .Create<TestConfigurator>();

        var services = TestConfigurator.LastServices!;
        Assert.Contains(services, d => d.ServiceType == typeof(ILoggerFactory) && d.ImplementationInstance == loggerFactory);
        Assert.Contains(services, d => d.ServiceType == typeof(string) && (string)d.ImplementationInstance! == "hello");
    }

    class TestConfigurator : IBusFactoryConfigurator
    {
        public static IServiceCollection? LastServices;

        public MyServiceBus.IMessageBus Build()
        {
            return new InMemoryTestHarness();
        }

        public void Configure(IServiceCollection services)
        {
            LastServices = services;
            services.AddSingleton<MyServiceBus.IMessageBus, InMemoryTestHarness>();
        }
    }
}
