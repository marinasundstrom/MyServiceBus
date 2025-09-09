using Microsoft.Extensions.DependencyInjection;
using System;

namespace MyServiceBus;

public class BusFactory
{
    public IMessageBus Create(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        var provider = services.BuildServiceProvider();
        foreach (var action in provider.GetServices<IPostBuildAction>())
            action.Execute(provider);
        return provider.GetRequiredService<IMessageBus>();
    }
}
