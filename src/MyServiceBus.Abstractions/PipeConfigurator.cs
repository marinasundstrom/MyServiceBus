using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public class PipeConfigurator<TContext>
    where TContext : class, PipeContext
{
    readonly List<Func<IServiceProvider?, IFilter<TContext>>> filters = new();

    public void UseFilter(IFilter<TContext> filter)
    {
        filters.Add(_ => filter);
    }

    [Throws(typeof(MissingMethodException))]
    public void UseFilter<TFilter>()
        where TFilter : class, IFilter<TContext>
    {
        filters.Add([Throws(typeof(MissingMethodException))] (provider) => provider != null
                ? (IFilter<TContext>)ActivatorUtilities.GetServiceOrCreateInstance(provider, typeof(TFilter))
                : Activator.CreateInstance<TFilter>()!);
    }

    public void UseExecute(Func<TContext, Task> callback)
    {
        UseFilter(new DelegateFilter(callback));
    }

    public void UseRetry(int retryCount, TimeSpan? delay = null)
    {
        UseFilter(new RetryFilter<TContext>(retryCount, delay));
    }

    public void UseMessageRetry(Action<RetryConfigurator> configure)
    {
        var rc = new RetryConfigurator();
        configure(rc);
        UseRetry(rc.RetryCount, rc.Delay);
    }

    public IPipe<TContext> Build(IServiceProvider? provider = null)
    {
        IPipe<TContext> next = Pipe.Empty<TContext>();
        for (var i = filters.Count - 1; i >= 0; i--)
        {
            var filter = filters[i](provider);
            next = new FilterPipe(filter, next);
        }

        return next;
    }

    class DelegateFilter : IFilter<TContext>
    {
        readonly Func<TContext, Task> callback;

        public DelegateFilter(Func<TContext, Task> callback)
        {
            this.callback = callback;
        }

        public async Task Send(TContext context, IPipe<TContext> next)
        {
            await callback(context);
            await next.Send(context);
        }
    }

    class FilterPipe : IPipe<TContext>
    {
        readonly IFilter<TContext> filter;
        readonly IPipe<TContext> next;

        public FilterPipe(IFilter<TContext> filter, IPipe<TContext> next)
        {
            this.filter = filter;
            this.next = next;
        }

        public Task Send(TContext context)
        {
            return filter.Send(context, next);
        }
    }

}
