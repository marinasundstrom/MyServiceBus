namespace MyServiceBus.AzureServiceBus.Tests;

public sealed class AzureServiceBusEmulatorFactAttribute : FactAttribute
{
    public AzureServiceBusEmulatorFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_AZURE_SERVICEBUS_EMULATOR_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set RUN_AZURE_SERVICEBUS_EMULATOR_TESTS=1 to run Azure Service Bus emulator tests.";
        }
    }
}
