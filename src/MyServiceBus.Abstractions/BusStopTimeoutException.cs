namespace MyServiceBus;

/// <summary>
/// Thrown when a bus cannot drain its active receive work within the configured shutdown timeout.
/// </summary>
public sealed class BusStopTimeoutException : TimeoutException
{
    public BusStopTimeoutException(TimeSpan timeout)
        : base($"The service bus did not stop within the configured timeout of {timeout}.")
    {
        Timeout = timeout;
    }

    public BusStopTimeoutException(TimeSpan timeout, Exception innerException)
        : base($"The service bus did not stop within the configured timeout of {timeout}.", innerException)
    {
        Timeout = timeout;
    }

    public TimeSpan Timeout { get; }
}
