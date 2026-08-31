namespace MyServiceBus;

public sealed class JobConsumerOptions
{
    public TimeSpan JobTimeout { get; private set; } = TimeSpan.FromMinutes(30);

    public int ConcurrentJobLimit { get; private set; } = 1;

    public int RetryCount { get; private set; }

    public TimeSpan? RetryDelay { get; private set; }

    public string? JobTypeName { get; private set; }

    public JobConsumerOptions SetJobTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "The job timeout must be greater than zero.");

        JobTimeout = timeout;
        return this;
    }

    public JobConsumerOptions SetConcurrentJobLimit(int limit)
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "The concurrent job limit must be greater than zero.");

        ConcurrentJobLimit = limit;
        return this;
    }

    public JobConsumerOptions SetRetry(Action<RetryConfigurator> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var retry = new RetryConfigurator();
        configure(retry);
        if (retry.RetryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(configure), "The retry count cannot be negative.");
        if (retry.Delay is { } delay && delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(configure), "The retry delay cannot be negative.");
        RetryCount = retry.RetryCount;
        RetryDelay = retry.Delay;
        return this;
    }

    public JobConsumerOptions SetJobTypeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        JobTypeName = name.Trim();
        return this;
    }
}
