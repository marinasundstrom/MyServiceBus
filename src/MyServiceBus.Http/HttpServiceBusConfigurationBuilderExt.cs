using MyServiceBus.Serialization;
using Microsoft.Extensions.DependencyInjection;

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
}
