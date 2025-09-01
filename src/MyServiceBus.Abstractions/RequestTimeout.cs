namespace MyServiceBus;

public struct RequestTimeout
{
#pragma warning disable THROW001 // Unhandled exception
    static TimeSpan defaultTimeout = TimeSpan.FromSeconds(30);
#pragma warning restore THROW001 // Unhandled exception

    public RequestTimeout(TimeSpan timeSpan)
    {
        this.TimeSpan = timeSpan;
    }

    public TimeSpan TimeSpan { get; }

    public static RequestTimeout None { get; } = new RequestTimeout(TimeSpan.Zero);

    public static RequestTimeout Default { get; } = new RequestTimeout(defaultTimeout);

    public static RequestTimeout After(TimeSpan timeSpan) => new RequestTimeout(timeSpan);
}