using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

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

    [Throws(typeof(InvalidOperationException))]
    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        try
        {
            using var scope = provider.CreateScope();
            var consumer = scope.ServiceProvider.GetRequiredService<TConsumer>();
            await consumer.Consume(context);
            await next.Send(context);
        }
        catch (Exception ex)
        {
            if (context is ConsumeContextImpl<TMessage> ctx)
            {
                await ctx.RespondFaultAsync(ex);
            }

            // TODO: Log instead
            throw new InvalidOperationException($"Consumer {typeof(TConsumer).Name} failed", ex);
        }
    }
}

