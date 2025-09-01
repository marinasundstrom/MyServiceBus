using System;
using System.Threading.Tasks;

namespace MyServiceBus;

public static class Pipe
{
    public static IPipe<TContext> Empty<TContext>()
        where TContext : class, PipeContext
        => new EmptyPipe<TContext>();

    public static IPipe<TContext> Execute<TContext>(Func<TContext, Task> callback)
        where TContext : class, PipeContext
        => new ExecutePipe<TContext>(callback);

    class EmptyPipe<TContext> : IPipe<TContext>
        where TContext : class, PipeContext
    {
        public Task Send(TContext context) => Task.CompletedTask;
    }

    class ExecutePipe<TContext> : IPipe<TContext>
        where TContext : class, PipeContext
    {
        readonly Func<TContext, Task> callback;

        public ExecutePipe(Func<TContext, Task> callback)
        {
            this.callback = callback;
        }

        public Task Send(TContext context) => callback(context);
    }
}
