namespace MyServiceBus.AmazonSqs.Tests;

internal sealed class AmazonSqsCloudFactAttribute : FactAttribute
{
    public AmazonSqsCloudFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_AMAZON_SQS_CLOUD_TESTS"), "1",
                StringComparison.Ordinal))
            Skip = "Set RUN_AMAZON_SQS_CLOUD_TESTS=1 to run AWS cloud tests.";
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_REGION")))
            Skip = "Set AWS_REGION to run AWS cloud tests.";
    }
}
