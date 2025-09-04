using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public static class MediatorServiceBusConfigurationBuilderExt
{
    public static IBusRegistrationConfigurator UsingMediator(this IBusRegistrationConfigurator builder, Action<IMediatorFactoryConfigurator>? c = null)
    {
        var configuration = new MediatorFactoryConfigurator();
        c?.Invoke(configuration);
        builder.Services.AddSingleton<IMediatorFactoryConfigurator>(configuration);
        builder.Services.AddSingleton<ITransportFactory, MediatorTransportFactory>();
        builder.Services.AddScoped<ISendEndpointProvider, SendEndpointProvider>();
        builder.Services.AddScoped<IPublishEndpointProvider, PublishEndpointProvider>();
        builder.Services.AddSingleton<IMessageBus>(sp => new MessageBus(
            sp.GetRequiredService<ITransportFactory>(),
            sp,
            sp.GetRequiredService<ISendPipe>(),
            sp.GetRequiredService<IPublishPipe>(),
            sp.GetRequiredService<IMessageSerializer>()));
        return builder;
    }
}

internal class MediatorFactoryConfigurator : IMediatorFactoryConfigurator
{
    public MediatorFactoryConfigurator()
    {
    }
}