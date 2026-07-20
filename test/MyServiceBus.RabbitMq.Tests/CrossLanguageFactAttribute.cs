namespace MyServiceBus.RabbitMq.Tests;

public sealed class CrossLanguageFactAttribute : FactAttribute
{
    public CrossLanguageFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_CROSS_LANGUAGE_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set RUN_CROSS_LANGUAGE_TESTS=1 to run the C#↔Java interoperability tests.";
        }
    }
}
