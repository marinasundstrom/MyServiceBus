using MyServiceBus.Serialization;

namespace MyServiceBus;

public interface IAzureServiceBusFactoryConfigurator
{
    string ConnectionString { get; }

    string? ManagementConnectionString { get; }

    AzureServiceBusTopologyMode TopologyMode { get; }

    int PrefetchCount { get; }

    IEndpointNameFormatter? EndpointNameFormatter { get; }

    IMessageEntityNameFormatter? EntityNameFormatter { get; }

    Func<string, string> TemporaryEndpointNameFormatter { get; }

    void Host(string connectionString);

    void ManagementEndpoint(string connectionString);

    void UsePreProvisionedTopology();

    void SetPrefetchCount(int prefetchCount);

    void SetEndpointNameFormatter(IEndpointNameFormatter formatter);

    void SetEntityNameFormatter(IMessageEntityNameFormatter formatter);

    void SetTemporaryEndpointNameFormatter(Func<string, string> formatter);

    void Message<T>(Action<AzureServiceBusMessageConfigurator> configure);

    string GetEntityName(Type messageType) => MyServiceBus.EntityNameFormatter.Format(messageType);

    void ReceiveEndpoint(string queueName, Action<AzureServiceBusReceiveEndpointConfigurator> configure);

    void ConfigureEndpoints(IBusRegistrationContext context);

    void SetConsumerFactory(Type consumerFactoryType);
}

public sealed class AzureServiceBusMessageConfigurator
{
    private readonly Type _messageType;
    private readonly IDictionary<Type, string> _entityNames;

    internal AzureServiceBusMessageConfigurator(Type messageType, IDictionary<Type, string> entityNames)
    {
        _messageType = messageType;
        _entityNames = entityNames;
    }

    public void SetEntityName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _entityNames[_messageType] = name;
    }

    public void SetEntityNameFormatter<T>(IMessageEntityNameFormatter<T> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _entityNames[_messageType] = formatter.FormatEntityName();
    }
}
