using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

internal sealed class ConsumerMethodMessageFilter<TMessage> : IFilter<ConsumeContext<TMessage>>
    where TMessage : class
{
    private readonly IServiceProvider provider;
    private readonly Func<IServiceProvider, ConsumeContext<TMessage>, Task> invoke;

    public ConsumerMethodMessageFilter(
        IServiceProvider provider,
        Func<IServiceProvider, ConsumeContext<TMessage>, Task> invoke)
    {
        this.provider = provider;
        this.invoke = invoke;
    }

    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        await using var scope = provider.CreateAsyncScope();
        var contextProvider = scope.ServiceProvider.GetService<ConsumeContextProvider>();
        if (contextProvider is not null)
            contextProvider.Context = context;

        try
        {
            await invoke(scope.ServiceProvider, context).ConfigureAwait(false);
            await next.Send(context).ConfigureAwait(false);
        }
        finally
        {
            if (contextProvider is not null)
                contextProvider.Context = null;
        }
    }
}
