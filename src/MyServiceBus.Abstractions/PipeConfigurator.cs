using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public class PipeConfigurator<TContext>
    where TContext : class, PipeContext
{
    readonly List<FilterRegistration> filters = new();

    public void UseFilter(IFilter<TContext> filter)
    {
        AddFilter(
            _ => filter,
            "filter",
            filter.GetType(),
            FilterLifetime.Instance);
    }

    public void UseFilter<TFilter>()
        where TFilter : class, IFilter<TContext>
    {
        AddFilter(
            provider => provider != null
                ? (IFilter<TContext>)ActivatorUtilities.GetServiceOrCreateInstance(provider, typeof(TFilter))
                : Activator.CreateInstance<TFilter>()!,
            "filter",
            typeof(TFilter),
            FilterLifetime.Pipe);
    }

    public void UseScopedFilter<TFilter>()
        where TFilter : class, IFilter<TContext>
    {
        AddFilter(
            provider => provider != null
                ? new ScopedFilter<TFilter>(provider)
                : throw new InvalidOperationException("A service provider is required to use a scoped filter"),
            "filter",
            typeof(TFilter),
            FilterLifetime.Scoped);
    }

    public void UseExecute(Func<TContext, Task> callback)
    {
        AddFilter(
            _ => new DelegateFilter(callback),
            "execute",
            null,
            FilterLifetime.Instance);
    }

    public void UseRetry(int retryCount, TimeSpan? delay = null)
    {
        if (retryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(retryCount));

        var configuration = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["retryCount"] = retryCount.ToString(CultureInfo.InvariantCulture)
        };
        if (delay.HasValue)
            configuration["delayMilliseconds"] = delay.Value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);

        AddFilter(
            _ => new RetryFilter<TContext>(retryCount, delay),
            "retry",
            typeof(RetryFilter<TContext>),
            FilterLifetime.Pipe,
            configuration);
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
            var filter = filters[i].Factory(provider);
            next = new FilterPipe(filter, next);
        }

        return next;
    }

    public PipelineDescriptor GetDescriptor()
    {
        return new PipelineDescriptor(
            filters.Select(x => x.Descriptor).ToArray());
    }

    void AddFilter(
        Func<IServiceProvider?, IFilter<TContext>> factory,
        string kind,
        Type? implementation,
        FilterLifetime lifetime,
        IReadOnlyDictionary<string, string>? configuration = null)
    {
        filters.Add(new FilterRegistration(
            factory,
            new FilterDescriptor(
                filters.Count,
                kind,
                implementation?.FullName,
                lifetime,
                configuration)));
    }

    sealed record FilterRegistration(
        Func<IServiceProvider?, IFilter<TContext>> Factory,
        FilterDescriptor Descriptor);

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

    sealed class ScopedFilter<TFilter> : IFilter<TContext>
        where TFilter : class, IFilter<TContext>
    {
        readonly IServiceProvider provider;

        public ScopedFilter(IServiceProvider provider)
        {
            this.provider = provider;
        }

        public async Task Send(TContext context, IPipe<TContext> next)
        {
            await using var scope = provider.CreateAsyncScope();
            var filter = scope.ServiceProvider.GetRequiredService<TFilter>();
            await filter.Send(context, next);
        }
    }

}
