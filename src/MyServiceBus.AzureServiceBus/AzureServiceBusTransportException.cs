namespace MyServiceBus;

public sealed class AzureServiceBusTransportException : Exception
{
    public AzureServiceBusTransportException(string operation, string entityName, Exception innerException)
        : base($"Azure Service Bus operation '{operation}' failed for entity '{entityName}'.", innerException)
    {
        Operation = operation;
        EntityName = entityName;
    }

    public string Operation { get; }

    public string EntityName { get; }
}
