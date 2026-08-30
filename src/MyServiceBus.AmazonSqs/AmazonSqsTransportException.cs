namespace MyServiceBus;

public sealed class AmazonSqsTransportException : Exception
{
    public AmazonSqsTransportException(string operation, string entityName, Exception innerException)
        : base($"Amazon SQS/SNS could not {operation} for entity '{entityName}'.", innerException)
    {
        Operation = operation;
        EntityName = entityName;
    }

    public string Operation { get; }
    public string EntityName { get; }
}
