namespace MyServiceBus;

public static class RegistrationConfiguratorExtensions
{
    public static IConsumerRegistrationConfigurator<T> AddConsumer<T>(this IRegistrationConfigurator configurator,
        Action<IConsumerConfigurator<T>> configure = null)
        where T : class, IConsumer
    {
        return null!;
    }
}
