using System.Threading;

namespace MyServiceBus;

public abstract class BasePipeContext : PipeContext
{
    protected BasePipeContext(CancellationToken cancellationToken = default)
    {
        CancellationToken = cancellationToken;
    }

    public CancellationToken CancellationToken { get; }
}

