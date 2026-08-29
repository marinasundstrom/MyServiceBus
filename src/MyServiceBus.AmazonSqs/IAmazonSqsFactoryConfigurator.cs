namespace MyServiceBus;

public interface IAmazonSqsFactoryConfigurator
{
    string Region { get; }
    string? ServiceUrl { get; }
    string Scope { get; }
    AmazonSqsTopologyMode TopologyMode { get; }
    int PrefetchCount { get; }
    int WaitTimeSeconds { get; }
    int VisibilityTimeoutSeconds { get; }
    IEndpointNameFormatter? EndpointNameFormatter { get; }
    IMessageEntityNameFormatter EntityNameFormatter { get; }

    void Host(string region);
    void LocalstackHost(string serviceUrl = "http://localhost:4566", string region = "us-east-1");
    void SetScope(string scope);
    void UsePreProvisionedTopology();
    void SetPrefetchCount(int prefetchCount);
    void SetWaitTimeSeconds(int waitTimeSeconds);
    void SetVisibilityTimeout(int visibilityTimeoutSeconds);
    void SetEndpointNameFormatter(IEndpointNameFormatter formatter);
    void SetEntityNameFormatter(IMessageEntityNameFormatter formatter);
    void Message<T>(Action<AmazonSqsMessageConfigurator> configure);
    string GetEntityName(Type messageType) => EntityNameFormatter.FormatEntityName(messageType);
    void ReceiveEndpoint(string queueName, Action<AmazonSqsReceiveEndpointConfigurator> configure);
    void ConfigureEndpoints(IBusRegistrationContext context);
    void SetConsumerFactory(Type consumerFactoryType);
}

public sealed class AmazonSqsMessageConfigurator
{
    private readonly Type _messageType;
    private readonly IDictionary<Type, string> _entityNames;

    internal AmazonSqsMessageConfigurator(Type messageType, IDictionary<Type, string> entityNames)
    {
        _messageType = messageType;
        _entityNames = entityNames;
    }

    public void SetEntityName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        AmazonSqsEntityName.Validate(name);
        _entityNames[_messageType] = name;
    }
}
