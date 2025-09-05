using System.Threading.Tasks;

namespace MyServiceBus;

public class ErrorTransportFilter<TMessage> : IFilter<ConsumeContext<TMessage>>
    where TMessage : class
{
    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        try
        {
            await next.Send(context);
        }
        catch
        {
            if (context is ConsumeContextImpl<TMessage> ctx)
            {
                var errorAddress = ctx.ReceiveContext.ErrorAddress;
                if (errorAddress != null)
                {
                    var endpoint = await ctx.GetSendEndpoint(errorAddress);
                    await endpoint.Send(ctx.Message, cancellationToken: context.CancellationToken);
                }
            }

            throw;
        }
    }
}
