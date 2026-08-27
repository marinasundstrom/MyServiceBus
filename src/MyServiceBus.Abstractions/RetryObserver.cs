namespace MyServiceBus;

public interface IRetryObserver
{
    void Observe(RetryEvent retryEvent);
}

public sealed record RetryEvent(
    PipeContext Context,
    int Attempt,
    int RetryLimit,
    bool Exhausted,
    TimeSpan? Delay,
    Exception Exception);
