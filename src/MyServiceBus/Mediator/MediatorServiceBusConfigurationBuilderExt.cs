using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public static class MediatorServiceBusConfigurationBuilderExt
{
    public static IBusRegistrationConfigurator UsingMediator(this IBusRegistrationConfigurator builder, Action<IMediatorFactoryConfigurator>? c = null)
    {
        var configuration = new MediatorFactoryConfigurator();
        c?.Invoke(configuration);
        builder.Services.AddSingleton<IMediatorFactoryConfigurator>(configuration);
        builder.Services.AddSingleton<ITransportFactory, MediatorTransportFactory>();
        builder.Services.AddSingleton<IMessageBus, MyMessageBus>();
        return builder;
    }
}

internal class MediatorFactoryConfigurator : IMediatorFactoryConfigurator
{
    public MediatorFactoryConfigurator()
    {
    }
}