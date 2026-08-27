using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;

namespace MyServiceBus;

public sealed class AzureServiceBusFactoryConfigurator : IAzureServiceBusFactoryConfigurator, IBusFactoryConfigurator
{
    internal const string EmulatorConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    private readonly Dictionary<Type, string> _entityNames = new();
    private readonly List<Action<IMessageBus, IServiceProvider>> _endpointActions = new();
    private Type _consumerFactoryType = typeof(DefaultConstructorConsumerFactory<>);

    public string ConnectionString { get; private set; } = EmulatorConnectionString;

    public string? ManagementConnectionString { get; private set; }

    public AzureServiceBusTopologyMode TopologyMode { get; private set; } = AzureServiceBusTopologyMode.Create;

    public int PrefetchCount { get; private set; }

    public IEndpointNameFormatter? EndpointNameFormatter { get; private set; }

    public IMessageEntityNameFormatter? EntityNameFormatter { get; private set; } =
        AzureServiceBusMessageEntityNameFormatter.Instance;

    public Func<string, string> TemporaryEndpointNameFormatter { get; private set; } = name => name;

    public void Host(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _ = ServiceBusConnectionStringProperties.Parse(connectionString);
        ConnectionString = connectionString;
    }

    public void ManagementEndpoint(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _ = ServiceBusConnectionStringProperties.Parse(connectionString);
        ManagementConnectionString = connectionString;
    }

    public void UsePreProvisionedTopology() => TopologyMode = AzureServiceBusTopologyMode.PreProvisioned;

    public void SetPrefetchCount(int prefetchCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(prefetchCount);
        PrefetchCount = prefetchCount;
    }

    public void SetEndpointNameFormatter(IEndpointNameFormatter formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        EndpointNameFormatter = formatter;
    }

    public void SetEntityNameFormatter(IMessageEntityNameFormatter formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        EntityNameFormatter = formatter;
    }

    public void SetTemporaryEndpointNameFormatter(Func<string, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        TemporaryEndpointNameFormatter = name =>
        {
            var formatted = formatter(name);
            return string.IsNullOrWhiteSpace(formatted)
                ? throw new InvalidOperationException("The temporary endpoint name formatter returned a blank name.")
                : formatted;
        };
    }

    public void Message<T>(Action<AzureServiceBusMessageConfigurator> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(new AzureServiceBusMessageConfigurator(typeof(T), _entityNames));
    }

    public string GetEntityName(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return _entityNames.TryGetValue(messageType, out var configuredName)
            ? configuredName
            : EntityNameFormatter!.FormatEntityName(messageType);
    }

    public void ReceiveEndpoint(string queueName, Action<AzureServiceBusReceiveEndpointConfigurator> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(configure);
        configure(new AzureServiceBusReceiveEndpointConfigurator(queueName, GetEntityName, _endpointActions));
    }

    public void SetConsumerFactory(Type consumerFactoryType)
    {
        ArgumentNullException.ThrowIfNull(consumerFactoryType);
        _consumerFactoryType = consumerFactoryType;
    }

    public void ConfigureEndpoints(IBusRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var registry = context.ServiceProvider.GetRequiredService<TopologyRegistry>();

        foreach (var consumer in registry.Consumers)
        {
            var messageType = consumer.Bindings.First().MessageType;
            var queueName = EndpointNameFormatter?.Format(messageType) ?? consumer.QueueName;
            var consumerType = consumer.ConsumerType;
            ReceiveEndpoint(queueName, endpoint =>
            {
                var method = typeof(AzureServiceBusReceiveEndpointConfigurator)
                    .GetMethod(nameof(AzureServiceBusReceiveEndpointConfigurator.ConfigureConsumer))!
                    .MakeGenericMethod(consumerType);
                method.Invoke(endpoint, [context]);
            });
        }
    }

    internal void Apply(IMessageBus bus, IServiceProvider provider)
    {
        foreach (var action in _endpointActions)
            action(bus, provider);
    }

    public IMessageBus Build()
    {
        var services = new ServiceCollection();
        Configure(services);
        var provider = services.BuildServiceProvider();
        foreach (var action in provider.GetServices<IPostBuildAction>())
            action.Execute(provider);
        return provider.GetRequiredService<IMessageBus>();
    }

    public void Configure(IServiceCollection services)
    {
        var configurator = new BusRegistrationConfigurator(services);
        configurator.Build();
        services.AddSingleton(typeof(IConsumerFactory<>), _consumerFactoryType);
        AzureServiceBusServiceCollectionExtensions.RegisterTransport(
            services,
            this,
            (context, cfg) => ((AzureServiceBusFactoryConfigurator)cfg).ConfigureEndpoints(context));
    }
}
