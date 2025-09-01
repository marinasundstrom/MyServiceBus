namespace MyServiceBus;

public interface IFilter<TContext>
    where TContext : class, PipeContext
{
    Task Send(TContext context, IPipe<TContext> next);
}
