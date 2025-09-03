using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyServiceBus;

public interface IConsumePipe
{
    Task Send(ConsumeContext context);
}

public class ConsumePipe<TMessage> : IConsumePipe
    where TMessage : class
{
    readonly IPipe<ConsumeContext<TMessage>> pipe;

    public ConsumePipe(IPipe<ConsumeContext<TMessage>> pipe)
    {
        this.pipe = pipe;
    }

    [Throws(typeof(InvalidCastException))]
    public Task Send(ConsumeContext context)
    {
        return pipe.Send((ConsumeContext<TMessage>)context);
    }
}

public class ConsumerMessageFilter<TConsumer, TMessage> : IFilter<ConsumeContext<TMessage>>
    where TConsumer : class, IConsumer<TMessage>
    where TMessage : class
{
    readonly IServiceProvider provider;

    public ConsumerMessageFilter(IServiceProvider provider)
    {
        this.provider = provider;
    }

    [Throws(typeof(Exception))]
    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        using var scope = provider.CreateScope();
        var consumer = scope.ServiceProvider.GetRequiredService<TConsumer>();
        await consumer.Consume(context);
        await next.Send(context);
    }
}

public class ConsumerFaultFilter<TConsumer, TMessage> : IFilter<ConsumeContext<TMessage>>
    where TConsumer : class
    where TMessage : class
{
    readonly IServiceProvider provider;

    public ConsumerFaultFilter(IServiceProvider provider)
    {
        this.provider = provider;
    }

    [Throws(typeof(InvalidOperationException))]
    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        try
        {
            await next.Send(context);
        }
        catch (Exception ex)
        {
            if (context is ConsumeContextImpl<TMessage> ctx)
            {
                await ctx.RespondFaultAsync(ex);
            }

            var logger = provider.GetService<ILogger<ConsumerFaultFilter<TConsumer, TMessage>>>();
            logger?.LogError(ex, "Consumer {Consumer} failed", typeof(TConsumer).Name);
        }
    }
}

