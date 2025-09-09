using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public class ScopeConsumerFactory<TConsumer> : IConsumerFactory<TConsumer>
    where TConsumer : class
{
    readonly IServiceProvider provider;

    public ScopeConsumerFactory(IServiceProvider provider)
    {
        this.provider = provider;
    }

    [Throws(typeof(Exception))]
    public async Task Send<TMessage>(ConsumeContext<TMessage> context,
        IPipe<ConsumerConsumeContext<TConsumer, TMessage>> next) where TMessage : class
    {
        using var scope = provider.CreateScope();
        var contextProvider = scope.ServiceProvider.GetService<ConsumeContextProvider>();
        if (contextProvider != null)
            contextProvider.Context = context;

        try
        {
            var consumer = scope.ServiceProvider.GetRequiredService<TConsumer>();
            var consumerContext = new ConsumerConsumeContextImpl<TConsumer, TMessage>(consumer, context);
            await next.Send(consumerContext);
        }
        finally
        {
            if (contextProvider != null)
                contextProvider.Context = null;
        }
    }
}
