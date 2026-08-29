using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using MyServiceBus.AzureServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus;

public sealed class AzureServiceBusTransportFactory : ITransportFactory
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusAdministrationClient? _administrationClient;
    private readonly AzureServiceBusTopologyMode _topologyMode;
    private readonly int _defaultPrefetchCount;
    private readonly Func<Type, string> _entityNameResolver;
    private readonly Func<string, string> _temporaryEndpointNameFormatter;
    private readonly Uri _baseAddress;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly IInboundMessageResolver _inboundMessageResolver;
    private readonly ConcurrentDictionary<string, ISendTransport> _sendTransports = new(StringComparer.Ordinal);

    public AzureServiceBusTransportFactory(
        ServiceBusClient client,
        IAzureServiceBusFactoryConfigurator configurator,
        IInboundMessageResolver? inboundMessageResolver = null,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(configurator);
        _client = client;
        _topologyMode = configurator.TopologyMode;
        _defaultPrefetchCount = configurator.PrefetchCount;
        _entityNameResolver = configurator.GetEntityName;
        _temporaryEndpointNameFormatter = configurator.TemporaryEndpointNameFormatter;
        _baseAddress = GetEndpoint(configurator.ConnectionString);
        _loggerFactory = loggerFactory;
        _inboundMessageResolver = inboundMessageResolver ?? new InboundMessageResolver();
        if (_topologyMode == AzureServiceBusTopologyMode.Create)
        {
            _administrationClient = new ServiceBusAdministrationClient(
                configurator.ManagementConnectionString ?? configurator.ConnectionString);
        }
    }

    public TransportCapabilityDescriptor Capabilities => TransportCapabilityDescriptors.AzureServiceBus;

    public string GetPublishEntityName(Type messageType) => _entityNameResolver(messageType);

    public Uri GetPublishAddress(string entityName) => CreateAddress(entityName, topic: true);

    public Uri GetPublishAddress(Type messageType) => GetPublishAddress(GetPublishEntityName(messageType));

    public Uri GetTemporaryEndpointAddress(string endpointName) =>
        CreateAddress(FormatTemporaryEndpointName(endpointName), topic: false, "temporary=true");

    public Uri GetErrorAddress(string endpointName) => CreateAddress(endpointName + "_error", topic: false);

    public Uri GetFaultAddress(string endpointName) => CreateAddress(endpointName + "_fault", topic: true);

    public Task<ISendTransport> GetSendTransport(
        Uri address,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        cancellationToken.ThrowIfCancellationRequested();
        var destination = AzureServiceBusEndpointAddress.Parse(address);
        var key = $"{destination.Kind}:{destination.EntityName}";
        var transport = _sendTransports.GetOrAdd(
            key,
            _ => new AzureServiceBusSendTransport(
                _client.CreateSender(destination.EntityName),
                destination.EntityName));
        return Task.FromResult(transport);
    }

    public async Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTransportTopology topology,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var projected = AzureServiceBusReceiveEndpointTopology.Project(MapTemporaryEndpoint(topology));
        if (_topologyMode == AzureServiceBusTopologyMode.Create)
        {
            try
            {
                await EnsureTopology(projected, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException
                                               and not AzureServiceBusTransportException)
            {
                throw new AzureServiceBusTransportException(
                    "provision topology",
                    projected.QueueName,
                    exception);
            }
        }

        var prefetchCount = projected.PrefetchCount > 0 ? projected.PrefetchCount : _defaultPrefetchCount;
        var processor = _client.CreateProcessor(
            projected.QueueName,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = projected.ConcurrentMessageLimit,
                PrefetchCount = prefetchCount
            });

        return new AzureServiceBusReceiveTransport(
            processor,
            _client.CreateSender(projected.QueueName + "_skipped"),
            projected.QueueName,
            handler,
            isMessageTypeRegistered,
            projected.Temporary ? null : GetErrorAddress(projected.QueueName),
            projected.Temporary ? null : GetFaultAddress(projected.QueueName),
            _inboundMessageResolver,
            _loggerFactory?.CreateLogger<AzureServiceBusReceiveTransport>());
    }

    private async Task EnsureTopology(
        AzureServiceBusReceiveEndpointTopology topology,
        CancellationToken cancellationToken)
    {
        var administrationClient = _administrationClient
            ?? throw new InvalidOperationException("Azure Service Bus administration client is not configured.");

        await EnsureQueue(administrationClient, topology.QueueName, topology.Temporary, cancellationToken)
            .ConfigureAwait(false);

        if (!topology.Temporary)
        {
            await EnsureQueue(administrationClient, topology.QueueName + "_error", false, cancellationToken)
                .ConfigureAwait(false);
            await EnsureQueue(administrationClient, topology.QueueName + "_skipped", false, cancellationToken)
                .ConfigureAwait(false);
            await EnsureTopic(administrationClient, topology.QueueName + "_fault", cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var binding in topology.Temporary ? [] : topology.Bindings)
        {
            await EnsureTopic(administrationClient, binding.EntityName, cancellationToken).ConfigureAwait(false);
            await EnsureSubscription(
                    administrationClient,
                    binding.EntityName,
                    topology.QueueName,
                    topology.QueueName,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private ReceiveEndpointTransportTopology MapTemporaryEndpoint(ReceiveEndpointTransportTopology topology)
    {
        if (!topology.Temporary)
            return topology;

        return new ReceiveEndpointTransportTopology(
            FormatTemporaryEndpointName(topology.Name),
            topology.Durable,
            topology.Temporary,
            topology.PrefetchCount,
            topology.Bindings,
            topology.TransportOptions,
            topology.ConcurrentMessageLimit);
    }

    private string FormatTemporaryEndpointName(string endpointName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        return _temporaryEndpointNameFormatter(endpointName);
    }

    private static async Task EnsureQueue(
        ServiceBusAdministrationClient client,
        string name,
        bool temporary,
        CancellationToken cancellationToken)
    {
        if ((await client.QueueExistsAsync(name, cancellationToken).ConfigureAwait(false)).Value)
            return;

        var options = new CreateQueueOptions(name);
        if (temporary)
            options.AutoDeleteOnIdle = TimeSpan.FromMinutes(5);

        try
        {
            await client.CreateQueueAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
        }
    }

    private static async Task EnsureTopic(
        ServiceBusAdministrationClient client,
        string name,
        CancellationToken cancellationToken)
    {
        if ((await client.TopicExistsAsync(name, cancellationToken).ConfigureAwait(false)).Value)
            return;

        try
        {
            await client.CreateTopicAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
        }
    }

    private static async Task EnsureSubscription(
        ServiceBusAdministrationClient client,
        string topic,
        string subscription,
        string forwardTo,
        CancellationToken cancellationToken)
    {
        if ((await client.SubscriptionExistsAsync(topic, subscription, cancellationToken).ConfigureAwait(false)).Value)
            return;

        var options = new CreateSubscriptionOptions(topic, subscription)
        {
            ForwardTo = forwardTo
        };
        try
        {
            await client.CreateSubscriptionAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
        }
    }

    private Uri CreateAddress(string entityName, bool topic, string? extraQuery = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        var builder = new UriBuilder(new Uri(_baseAddress, Uri.EscapeDataString(entityName)));
        var query = topic ? "type=topic" : string.Empty;
        if (!string.IsNullOrWhiteSpace(extraQuery))
            query = string.IsNullOrEmpty(query) ? extraQuery : query + "&" + extraQuery;
        builder.Query = query;
        return builder.Uri;
    }

    internal static Uri GetEndpoint(string connectionString)
    {
        var properties = ServiceBusConnectionStringProperties.Parse(connectionString);
        var endpoint = properties.Endpoint;
        var builder = new UriBuilder(endpoint)
        {
            Path = "/",
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri;
    }
}

internal enum AzureServiceBusEntityKind
{
    Queue,
    Topic
}

internal readonly record struct AzureServiceBusEndpointAddress(
    string EntityName,
    AzureServiceBusEntityKind Kind)
{
    public static AzureServiceBusEndpointAddress Parse(Uri address)
    {
        string entityName;
        AzureServiceBusEntityKind kind;
        if (address.Scheme.Equals("queue", StringComparison.OrdinalIgnoreCase))
        {
            entityName = ParseLogical(address, "queue:");
            kind = AzureServiceBusEntityKind.Queue;
        }
        else if (address.Scheme.Equals("topic", StringComparison.OrdinalIgnoreCase)
                 || address.Scheme.Equals("exchange", StringComparison.OrdinalIgnoreCase))
        {
            entityName = ParseLogical(address, address.Scheme + ":");
            kind = AzureServiceBusEntityKind.Topic;
        }
        else if (address.Scheme.Equals("sb", StringComparison.OrdinalIgnoreCase))
        {
            entityName = Uri.UnescapeDataString(address.AbsolutePath.Trim('/'));
            var type = QueryValue(address, "type");
            kind = type?.ToLowerInvariant() switch
            {
                null or "" => AzureServiceBusEntityKind.Queue,
                "topic" => AzureServiceBusEntityKind.Topic,
                _ => throw new ArgumentException(
                    $"Azure Service Bus entity type '{type}' is not supported.", nameof(address))
            };
        }
        else
        {
            throw new ArgumentException(
                $"Azure Service Bus address scheme '{address.Scheme}' is not supported.", nameof(address));
        }

        if (string.IsNullOrWhiteSpace(entityName))
            throw new ArgumentException("Azure Service Bus entity name cannot be blank.", nameof(address));

        return new AzureServiceBusEndpointAddress(entityName, kind);
    }

    private static string ParseLogical(Uri address, string prefix)
    {
        var value = address.OriginalString[prefix.Length..];
        var queryIndex = value.IndexOf('?');
        return queryIndex >= 0 ? value[..queryIndex] : value;
    }

    private static string? QueryValue(Uri address, string key)
    {
        foreach (var item in address.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = item.Split('=', 2);
            if (pair.Length == 2 && pair[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(pair[1]);
        }

        return null;
    }
}
