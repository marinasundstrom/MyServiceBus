namespace MyServiceBus.AzureServiceBus.Tests;

public sealed class AzureServiceBusCloudFactAttribute : FactAttribute
{
    public AzureServiceBusCloudFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_AZURE_SERVICEBUS_CLOUD_TESTS"),
                "1",
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CLOUD_CONNECTION_STRING")))
        {
            Skip = "Set RUN_AZURE_SERVICEBUS_CLOUD_TESTS=1 and AZURE_SERVICEBUS_CLOUD_CONNECTION_STRING to run Azure Service Bus cloud tests.";
        }
    }
}
