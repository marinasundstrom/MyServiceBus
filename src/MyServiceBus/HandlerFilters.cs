using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyServiceBus;

public class HandlerMessageFilter<TMessage> : IFilter<ConsumeContext<TMessage>>
    where TMessage : class
{
    readonly Func<ConsumeContext<TMessage>, Task> handler;

    public HandlerMessageFilter(Func<ConsumeContext<TMessage>, Task> handler)
    {
        this.handler = handler;
    }

    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        await handler(context).ConfigureAwait(false);
        await next.Send(context).ConfigureAwait(false);
    }
}

public class HandlerFaultFilter<TMessage> : IFilter<ConsumeContext<TMessage>>
    where TMessage : class
{
    readonly IServiceProvider provider;

    public HandlerFaultFilter(IServiceProvider provider)
    {
        this.provider = provider;
    }

    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        try
        {
            await next.Send(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (context is ConsumeContextImpl<TMessage> ctx)
            {
                await ctx.RespondFaultAsync(ex).ConfigureAwait(false);
            }

            var logger = provider.GetService<ILogger<HandlerFaultFilter<TMessage>>>();
            logger?.LogError(ex, "Handler faulted");
        }
    }
}

