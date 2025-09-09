using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyServiceBus;

public static class BusFactoryExtensions
{
    public static BusFactoryBuilder WithLoggerFactory(this IBusFactory factory, ILoggerFactory loggerFactory)
        => new BusFactoryBuilder(factory).WithLoggerFactory(loggerFactory);

    public static BusFactoryBuilder ConfigureServices(this IBusFactory factory, Action<IServiceCollection> configure)
        => new BusFactoryBuilder(factory).ConfigureServices(configure);
}

