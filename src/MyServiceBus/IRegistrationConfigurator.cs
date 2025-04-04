using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public interface IRegistrationConfigurator
//: IServiceCollection
{
        void AddConsumer<T>() where T : class, IConsumer;

        /*
        IConsumerRegistrationConfigurator<T> AddConsumer<T>(Action<IRegistrationContext, IConsumerConfigurator<T>> configure = null)
                where T : class, IConsumer;

    IConsumerRegistrationConfigurator<T> AddConsumer<T>(Type consumerDefinitionType,
        Action<IRegistrationContext, IConsumerConfigurator<T>> configure = null)
        where T : class, IConsumer;
        */
}
