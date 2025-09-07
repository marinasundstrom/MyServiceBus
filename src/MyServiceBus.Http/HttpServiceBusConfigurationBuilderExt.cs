using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public static class HttpServiceBusConfigurationBuilderExt
{
    public static IBusRegistrationConfigurator UsingHttp(
        this IBusRegistrationConfigurator builder,
        Uri baseAddress)
    {
        builder.Services.AddSingleton<HttpClient>();

        builder.Services.AddSingleton<ITransportFactory>(sp =>
            new HttpTransportFactory(
                sp.GetRequiredService<HttpClient>(),
                sp.GetRequiredService<IMessageSerializer>()));

        builder.Services.AddSingleton<IMessageBus>(sp => new MessageBus(
            sp.GetRequiredService<ITransportFactory>(),
            sp,
            sp.GetRequiredService<ISendPipe>(),
            sp.GetRequiredService<IPublishPipe>(),
            sp.GetRequiredService<IMessageSerializer>(),
            baseAddress));

        return builder;
    }
}
