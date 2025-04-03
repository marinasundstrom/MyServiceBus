namespace MyServiceBus;

public interface PipeContext
{
    CancellationToken CancellationToken { get; }
}
