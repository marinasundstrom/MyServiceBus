namespace MyServiceBus;

public interface IPipe<TContext>
    where TContext : class, PipeContext
{
    Task Send(TContext context);
}
