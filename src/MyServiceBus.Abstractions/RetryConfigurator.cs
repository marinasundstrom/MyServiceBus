using System;

namespace MyServiceBus;

public class RetryConfigurator
{
    public int RetryCount { get; private set; }
    public TimeSpan? Delay { get; private set; }

    public void Immediate(int retryCount)
    {
        RetryCount = retryCount;
        Delay = null;
    }

    public void Interval(int retryCount, TimeSpan interval)
    {
        RetryCount = retryCount;
        Delay = interval;
    }
}
