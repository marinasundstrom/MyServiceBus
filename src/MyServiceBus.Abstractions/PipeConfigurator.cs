using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MyServiceBus;

public class PipeConfigurator<TContext>
    where TContext : class, PipeContext
{
    readonly List<IFilter<TContext>> filters = new List<IFilter<TContext>>();

    public void UseFilter(IFilter<TContext> filter)
    {
        filters.Add(filter);
    }

    public void UseExecute(Func<TContext, Task> callback)
    {
        UseFilter(new DelegateFilter(callback));
    }

    public IPipe<TContext> Build()
    {
        IPipe<TContext> next = Pipe.Empty<TContext>();
        for (var i = filters.Count - 1; i >= 0; i--)
            next = new FilterPipe(filters[i], next);

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
