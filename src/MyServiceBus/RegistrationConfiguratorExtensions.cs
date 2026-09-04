namespace MyServiceBus;

public static class RegistrationConfiguratorExtensions
{
    public static IConsumerRegistrationConfigurator<T> AddConsumer<T>(this IRegistrationConfigurator configurator,
        Action<IConsumerConfigurator<T>>? configure = null)
        where T : class, IConsumer
    {
        ArgumentNullException.ThrowIfNull(configurator);
        var definition = new ConsumerDefinition<T>();
        configure?.Invoke(definition);
        configurator.AddConsumer(definition);
        return new ConsumerRegistrationConfigurator<T>(definition);
    }
}
