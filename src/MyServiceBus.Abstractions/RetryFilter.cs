using System;
using System.Threading.Tasks;

namespace MyServiceBus;

public class RetryFilter<TContext> : IFilter<TContext>
    where TContext : class, PipeContext
{
    private readonly int retryCount;
    private readonly TimeSpan? delay;

    public RetryFilter(int retryCount, TimeSpan? delay = null)
    {
        if (retryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(retryCount));

        this.retryCount = retryCount;
        this.delay = delay;
    }

    public async Task Send(TContext context, IPipe<TContext> next)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await next.Send(context);
                break;
            }
            catch
            {
                if (attempt >= retryCount)
                    throw;

                if (delay.HasValue)
                    await Task.Delay(delay.Value, context.CancellationToken);
            }
        }
    }
}
