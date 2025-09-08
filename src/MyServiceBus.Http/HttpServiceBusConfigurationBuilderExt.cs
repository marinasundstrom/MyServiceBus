using MyServiceBus.Serialization;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace MyServiceBus;

public static class HttpServiceBusConfigurationBuilderExt
{
    public static IBusRegistrationConfigurator UsingHttp(this IBusRegistrationConfigurator builder, Uri baseAddress)
    {
        builder.Services.AddSingleton<ITransportFactory, HttpTransportFactory>();
        builder.Services.AddSingleton<IMessageBus>(sp => new MessageBus(
            sp.GetRequiredService<ITransportFactory>(),
            sp,
            sp.GetRequiredService<ISendPipe>(),
            sp.GetRequiredService<IPublishPipe>(),
            sp.GetRequiredService<IMessageSerializer>(),
            baseAddress,
            sp.GetRequiredService<ISendContextFactory>(),
            sp.GetRequiredService<IPublishContextFactory>()));
        return builder;
    }

    public static IBusRegistrationConfigurator UsingHttp(
        this IBusRegistrationConfigurator builder,
        Uri baseAddress,
        Action<IBusRegistrationContext, IHttpFactoryConfigurator> configure)
    {
        var httpConfigurator = new HttpFactoryConfigurator(baseAddress);

        builder.Services.AddSingleton<IHttpFactoryConfigurator>(httpConfigurator);
        builder.Services.AddSingleton<IPostBuildAction>(new PostBuildHttpConfigureAction(configure, httpConfigurator));
        builder.Services.AddSingleton<ITransportFactory, HttpTransportFactory>();
        builder.Services.AddSingleton<IMessageBus>(sp => new MessageBus(
            sp.GetRequiredService<ITransportFactory>(),
            sp,
            sp.GetRequiredService<ISendPipe>(),
            sp.GetRequiredService<IPublishPipe>(),
            sp.GetRequiredService<IMessageSerializer>(),
            baseAddress,
            sp.GetRequiredService<ISendContextFactory>(),
            sp.GetRequiredService<IPublishContextFactory>()));
        return builder;
    }
}
