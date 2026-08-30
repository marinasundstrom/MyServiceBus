using Amazon;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;

namespace MyServiceBus;

public sealed class AmazonSqsFactoryConfigurator : IAmazonSqsFactoryConfigurator, IBusFactoryConfigurator
{
    private readonly Dictionary<Type, string> _entityNames = new();
    private readonly List<Action<IMessageBus, IServiceProvider>> _endpointActions = new();
    private Type _consumerFactoryType = typeof(DefaultConstructorConsumerFactory<>);

    public string Region { get; private set; } = RegionEndpoint.USEast1.SystemName;
    public string? ServiceUrl { get; private set; }
    public string Scope { get; private set; } = string.Empty;
    public AmazonSqsTopologyMode TopologyMode { get; private set; } = AmazonSqsTopologyMode.Create;
    public int PrefetchCount { get; private set; } = 10;
    public int WaitTimeSeconds { get; private set; } = 20;
    public int VisibilityTimeoutSeconds { get; private set; } = 30;
    public IEndpointNameFormatter? EndpointNameFormatter { get; private set; }
    public IMessageEntityNameFormatter EntityNameFormatter { get; private set; } = AmazonSqsMessageEntityNameFormatter.Instance;

    public void Host(string region)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        Region = region;
        ServiceUrl = null;
    }

    public void LocalstackHost(string serviceUrl = "http://localhost:4566", string region = "us-east-1")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out _))
            throw new ArgumentException("LocalStack service URL must be absolute.", nameof(serviceUrl));
        ServiceUrl = serviceUrl.TrimEnd('/');
        Region = region;
    }

    public void SetScope(string scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Length > 0)
            AmazonSqsEntityName.Validate(scope.TrimEnd('-', '_'));
        Scope = scope;
    }

    public void UsePreProvisionedTopology() => TopologyMode = AmazonSqsTopologyMode.PreProvisioned;

    public void SetPrefetchCount(int prefetchCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(prefetchCount, 1);
        PrefetchCount = prefetchCount;
    }

    public void SetWaitTimeSeconds(int waitTimeSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(waitTimeSeconds, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(waitTimeSeconds, 20);
        WaitTimeSeconds = waitTimeSeconds;
    }

    public void SetVisibilityTimeout(int visibilityTimeoutSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(visibilityTimeoutSeconds, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(visibilityTimeoutSeconds, 43200);
        VisibilityTimeoutSeconds = visibilityTimeoutSeconds;
    }

    public void SetEndpointNameFormatter(IEndpointNameFormatter formatter) =>
        EndpointNameFormatter = formatter ?? throw new ArgumentNullException(nameof(formatter));

    public void SetEntityNameFormatter(IMessageEntityNameFormatter formatter) =>
        EntityNameFormatter = formatter ?? throw new ArgumentNullException(nameof(formatter));

    public void Message<T>(Action<AmazonSqsMessageConfigurator> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(new AmazonSqsMessageConfigurator(typeof(T), _entityNames));
    }

    public string GetEntityName(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        var name = _entityNames.TryGetValue(messageType, out var configured)
            ? configured
            : EntityNameFormatter.FormatEntityName(messageType);
        return ApplyScope(name, topic: true);
    }

    public void ReceiveEndpoint(string queueName, Action<AmazonSqsReceiveEndpointConfigurator> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(new AmazonSqsReceiveEndpointConfigurator(ApplyScope(queueName, topic: false), GetEntityName, _endpointActions));
    }

    public void ConfigureEndpoints(IBusRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var consumer in context.ServiceProvider.GetRequiredService<TopologyRegistry>().Consumers)
            ReceiveEndpoint(consumer.ResolveEndpointName(EndpointNameFormatter), endpoint => endpoint.ConfigureConsumer(context, consumer));
    }

    public void SetConsumerFactory(Type consumerFactoryType) =>
        _consumerFactoryType = consumerFactoryType ?? throw new ArgumentNullException(nameof(consumerFactoryType));

    internal string ApplyScope(string name, bool topic)
    {
        if (topic)
            AmazonSqsEntityName.ValidateTopic(name);
        else
            AmazonSqsEntityName.Validate(name);
        var scoped = Scope + name;
        if (topic)
            AmazonSqsEntityName.ValidateTopic(scoped);
        else
            AmazonSqsEntityName.Validate(scoped);
        return scoped;
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
        var registration = new BusRegistrationConfigurator(services);
        registration.Build();
        services.AddSingleton(typeof(IConsumerFactory<>), _consumerFactoryType);
        AmazonSqsServiceCollectionExtensions.RegisterTransport(
            services, this, (context, cfg) => cfg.ConfigureEndpoints(context));
    }
}
