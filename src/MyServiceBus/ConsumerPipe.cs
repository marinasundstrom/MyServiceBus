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

    public Task Send(ConsumeContext context)
    {
        var typed = (ConsumeContext<TMessage>)context;
        _ = typed.Message; // Materialize before retry/fault filters or application code run.
        return pipe.Send(typed);
    }
}

public class ConsumerMessageFilter<TConsumer, TMessage> : IFilter<ConsumeContext<TMessage>>
    where TConsumer : class, IConsumer<TMessage>
    where TMessage : class
{
    readonly IConsumerFactory<TConsumer> factory;

    public ConsumerMessageFilter(IConsumerFactory<TConsumer> factory)
    {
        this.factory = factory;
    }

    public Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        return factory.Send(context, Pipe.Execute<ConsumerConsumeContext<TConsumer, TMessage>>(async consumerContext =>
        {
            await consumerContext.Consumer.Consume(consumerContext);
            await next.Send(consumerContext);
        }));
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
            logger?.LogError(ex, "Consumer {Consumer} faulted", typeof(TConsumer).Name);
            throw;
        }
    }
}

