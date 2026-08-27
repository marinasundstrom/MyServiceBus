using System;
using System.Threading.Tasks;

namespace MyServiceBus;

public class RetryFilter<TContext> : IFilter<TContext>
    where TContext : class, PipeContext
{
    private readonly int retryCount;
    private readonly TimeSpan? delay;
    private readonly IReadOnlyList<IRetryObserver> observers;

    public RetryFilter(int retryCount, TimeSpan? delay = null, IEnumerable<IRetryObserver>? observers = null)
    {
        if (retryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(retryCount));

        this.retryCount = retryCount;
        this.delay = delay;
        this.observers = observers?.ToArray() ?? [];
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
            catch (Exception exception)
            {
                var exhausted = attempt >= retryCount;
                Notify(context, attempt + 1, exhausted, exception);
                if (exhausted)
                    throw;

                if (delay.HasValue)
                    await Task.Delay(delay.Value, context.CancellationToken);
            }
        }
    }

    private void Notify(TContext context, int attempt, bool exhausted, Exception exception)
    {
        if (observers.Count == 0)
            return;

        var retryEvent = new RetryEvent(context, attempt, retryCount, exhausted, delay, exception);
        foreach (var observer in observers)
        {
            try
            {
                observer.Observe(retryEvent);
            }
            catch
            {
                // Retry observers are diagnostic and cannot change retry behavior.
            }
        }
    }
}
