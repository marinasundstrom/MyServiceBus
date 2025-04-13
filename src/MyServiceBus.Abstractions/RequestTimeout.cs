namespace MyServiceBus;

public struct RequestTimeout
{
    static TimeSpan defaultTimeout = TimeSpan.FromSeconds(30);

    public RequestTimeout(TimeSpan timeSpan)
    {
        this.TimeSpan = timeSpan;
    }

    public TimeSpan TimeSpan { get; } = defaultTimeout;

    public static RequestTimeout None { get; } = new RequestTimeout(TimeSpan.Zero);

    public static RequestTimeout Default { get; } = new RequestTimeout(defaultTimeout);

    public static RequestTimeout After(TimeSpan timeSpan) => new RequestTimeout(timeSpan);
}