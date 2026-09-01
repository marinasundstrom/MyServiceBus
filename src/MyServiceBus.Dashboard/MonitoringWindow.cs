namespace MyServiceBus.Dashboard;

public static class MonitoringWindow
{
    public const int DefaultSeconds = 300;

    public static IReadOnlyList<int> SupportedSeconds { get; } = [60, 300, 900, 3600, 21600, 86400];

    public static bool IsSupported(int value) => SupportedSeconds.Contains(value);

    public static int Normalize(int? value, int fallback = DefaultSeconds)
        => value is { } seconds && IsSupported(seconds) ? seconds : fallback;

    public static int BucketSeconds(int windowSeconds) => windowSeconds switch
    {
        <= 300 => 5,
        <= 900 => 15,
        <= 3600 => 60,
        <= 21600 => 300,
        _ => 900
    };

    public static string Format(int seconds) => seconds switch
    {
        60 => "1 minute",
        300 => "5 minutes",
        900 => "15 minutes",
        3600 => "1 hour",
        21600 => "6 hours",
        86400 => "24 hours",
        _ => TimeSpan.FromSeconds(seconds).ToString()
    };
}
