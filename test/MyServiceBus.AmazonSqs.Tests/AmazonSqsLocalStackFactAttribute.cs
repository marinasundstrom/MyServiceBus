namespace MyServiceBus.AmazonSqs.Tests;

public sealed class AmazonSqsLocalStackFactAttribute : FactAttribute
{
    public AmazonSqsLocalStackFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_AMAZON_SQS_LOCALSTACK_TESTS"),
                "1",
                StringComparison.Ordinal))
            Skip = "Set RUN_AMAZON_SQS_LOCALSTACK_TESTS=1 to run Amazon SQS/SNS LocalStack tests.";
    }
}
