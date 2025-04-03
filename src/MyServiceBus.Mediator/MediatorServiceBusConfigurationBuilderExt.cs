namespace MyServiceBus;

public static class MediatorServiceBusConfigurationBuilderExt
{
    public static IBusRegistrationConfigurator UsingMediator(this IBusRegistrationConfigurator builder, Action<IMediatorFactoryConfigurator>? c = null)
    {
        var configuration = new MediatorFactoryConfigurator();
        c?.Invoke(configuration);

        return builder;
    }
}

internal class MediatorFactoryConfigurator : IMediatorFactoryConfigurator
{
    public MediatorFactoryConfigurator()
    {
    }
}