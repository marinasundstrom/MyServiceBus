namespace MyServiceBus;

using Microsoft.Extensions.DependencyInjection;

public interface IBusFactoryConfigurator
{
    IMessageBus Build();
    void Configure(IServiceCollection services);
}
