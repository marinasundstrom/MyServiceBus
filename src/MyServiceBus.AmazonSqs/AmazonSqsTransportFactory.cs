using System.Collections.Concurrent;
using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus;

public sealed class AmazonSqsTransportFactory : ITransportFactory
{
    private readonly IAmazonSQS _sqs;
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly IAmazonSqsFactoryConfigurator _configurator;
    private readonly IInboundMessageResolver _inboundMessageResolver;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ConcurrentDictionary<string, Task<ISendTransport>> _sendTransports = new(StringComparer.Ordinal);
    private readonly Uri _baseAddress;

    public AmazonSqsTransportFactory(
        IAmazonSQS sqs,
        IAmazonSimpleNotificationService sns,
        IAmazonSqsFactoryConfigurator configurator,
        IInboundMessageResolver? inboundMessageResolver = null,
        ILoggerFactory? loggerFactory = null)
    {
        _sqs = sqs ?? throw new ArgumentNullException(nameof(sqs));
        _sns = sns ?? throw new ArgumentNullException(nameof(sns));
        _configurator = configurator ?? throw new ArgumentNullException(nameof(configurator));
        _inboundMessageResolver = inboundMessageResolver ?? new InboundMessageResolver();
        _loggerFactory = loggerFactory;
        _baseAddress = new Uri($"amazonsqs://{configurator.Region}/");
    }

    public TransportCapabilityDescriptor Capabilities => TransportCapabilityDescriptors.AmazonSqs;
    public string GetPublishEntityName(Type messageType) => _configurator.GetEntityName(messageType);
    public Uri GetPublishAddress(string entityName) => CreateAddress(entityName, true);
    public Uri GetTemporaryEndpointAddress(string endpointName) => CreateAddress(endpointName, false, "temporary=true");
    public Uri GetErrorAddress(string endpointName) => CreateAddress(AmazonSqsEntityName.Companion(endpointName, "_error"), false);
    public Uri GetFaultAddress(string endpointName) => CreateAddress(AmazonSqsEntityName.Companion(endpointName, "_fault"), true);

    public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (address.Scheme.Equals("amazonsqs", StringComparison.OrdinalIgnoreCase) &&
            !address.Host.Equals(_configurator.Region, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Amazon SQS address region '{address.Host}' does not match configured region '{_configurator.Region}'.",
                nameof(address));
        var endpoint = AmazonSqsEndpointAddress.Parse(address);
        return _sendTransports.GetOrAdd($"{endpoint.Kind}:{endpoint.EntityName}",
            _ => CreateSendTransport(endpoint, cancellationToken));
    }

    public async Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTransportTopology topology,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var projected = AmazonSqsReceiveEndpointTopology.Project(topology);
        try
        {
            var queueUrl = _configurator.TopologyMode == AmazonSqsTopologyMode.Create
                ? (await _sqs.CreateQueueAsync(new CreateQueueRequest
                {
                    QueueName = projected.QueueName,
                    Attributes = new Dictionary<string, string>
                    {
                        [QueueAttributeName.VisibilityTimeout] = _configurator.VisibilityTimeoutSeconds.ToString(
                            System.Globalization.CultureInfo.InvariantCulture)
                    }
                }, cancellationToken).ConfigureAwait(false)).QueueUrl
                : (await _sqs.GetQueueUrlAsync(projected.QueueName, cancellationToken).ConfigureAwait(false)).QueueUrl;

            string? skippedQueueUrl = null;
            if (!projected.Temporary)
            {
                var skippedName = AmazonSqsEntityName.Companion(projected.QueueName, "_skipped");
                var errorName = AmazonSqsEntityName.Companion(projected.QueueName, "_error");
                var faultName = AmazonSqsEntityName.Companion(projected.QueueName, "_fault");
                skippedQueueUrl = _configurator.TopologyMode == AmazonSqsTopologyMode.Create
                    ? await EnsureQueue(skippedName, cancellationToken).ConfigureAwait(false)
                    : (await _sqs.GetQueueUrlAsync(skippedName, cancellationToken).ConfigureAwait(false)).QueueUrl;
                if (_configurator.TopologyMode == AmazonSqsTopologyMode.Create)
                {
                    await EnsureQueue(errorName, cancellationToken).ConfigureAwait(false);
                    await ResolveTopicArn(faultName, cancellationToken).ConfigureAwait(false);
                    await EnsureSubscriptions(queueUrl, projected.Bindings.Select(x => x.EntityName), cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            return new AmazonSqsReceiveTransport(
                _sqs, queueUrl, skippedQueueUrl, projected.QueueName, projected.Temporary,
                _configurator.WaitTimeSeconds, _configurator.VisibilityTimeoutSeconds,
                projected.PrefetchCount > 0 ? projected.PrefetchCount : _configurator.PrefetchCount,
                projected.ConcurrentMessageLimit, handler, isMessageTypeRegistered,
                projected.Temporary ? null : GetErrorAddress(projected.QueueName),
                projected.Temporary ? null : GetFaultAddress(projected.QueueName),
                _inboundMessageResolver,
                _loggerFactory?.CreateLogger<AmazonSqsReceiveTransport>());
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not AmazonSqsTransportException)
        {
            throw new AmazonSqsTransportException("provision topology", projected.QueueName, exception);
        }
    }

    private async Task<ISendTransport> CreateSendTransport(
        AmazonSqsEndpointAddress endpoint,
        CancellationToken cancellationToken)
    {
        var destination = endpoint.Kind == AmazonSqsEntityKind.Topic
            ? await ResolveTopicArn(endpoint.EntityName, cancellationToken).ConfigureAwait(false)
            : _configurator.TopologyMode == AmazonSqsTopologyMode.Create
                ? await EnsureQueue(endpoint.EntityName, cancellationToken).ConfigureAwait(false)
                : (await _sqs.GetQueueUrlAsync(endpoint.EntityName, cancellationToken).ConfigureAwait(false)).QueueUrl;
        return new AmazonSqsSendTransport(_sqs, _sns, endpoint.Kind, destination, endpoint.EntityName);
    }

    private async Task<string> EnsureQueue(string name, CancellationToken cancellationToken) =>
        (await _sqs.CreateQueueAsync(name, cancellationToken).ConfigureAwait(false)).QueueUrl;

    private async Task<string> ResolveTopicArn(string name, CancellationToken cancellationToken)
    {
        if (_configurator.TopologyMode == AmazonSqsTopologyMode.Create)
            return (await _sns.CreateTopicAsync(name, cancellationToken).ConfigureAwait(false)).TopicArn;

        string? nextToken = null;
        do
        {
            var response = await _sns.ListTopicsAsync(new ListTopicsRequest { NextToken = nextToken }, cancellationToken)
                .ConfigureAwait(false);
            var topic = (response.Topics ?? []).FirstOrDefault(x =>
                x.TopicArn.EndsWith(':' + name, StringComparison.Ordinal));
            if (topic is not null)
                return topic.TopicArn;
            nextToken = response.NextToken;
        } while (!string.IsNullOrEmpty(nextToken));
        throw new InvalidOperationException($"Pre-provisioned SNS topic '{name}' was not found.");
    }

    private async Task EnsureSubscriptions(
        string queueUrl,
        IEnumerable<string> topicNames,
        CancellationToken cancellationToken)
    {
        var attributes = await _sqs.GetQueueAttributesAsync(queueUrl, [QueueAttributeName.QueueArn], cancellationToken)
            .ConfigureAwait(false);
        var queueArn = attributes.QueueARN;
        foreach (var topicName in topicNames.Distinct(StringComparer.Ordinal))
        {
            var topicArn = await ResolveTopicArn(topicName, cancellationToken).ConfigureAwait(false);
            await EnsureQueuePolicy(queueUrl, queueArn, topicArn, cancellationToken).ConfigureAwait(false);
            var subscription = await _sns.SubscribeAsync(new SubscribeRequest
            {
                TopicArn = topicArn,
                Protocol = "sqs",
                Endpoint = queueArn,
                Attributes = new Dictionary<string, string> { ["RawMessageDelivery"] = "true" },
                ReturnSubscriptionArn = true
            }, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(subscription.SubscriptionArn) && subscription.SubscriptionArn != "pending confirmation")
                await _sns.SetSubscriptionAttributesAsync(
                    subscription.SubscriptionArn, "RawMessageDelivery", "true", cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnsureQueuePolicy(
        string queueUrl,
        string queueArn,
        string topicArn,
        CancellationToken cancellationToken)
    {
        var current = await _sqs.GetQueueAttributesAsync(queueUrl, [QueueAttributeName.Policy], cancellationToken)
            .ConfigureAwait(false);
        var statements = new List<Dictionary<string, object?>>();
        if (!string.IsNullOrWhiteSpace(current.Policy))
        {
            using var document = JsonDocument.Parse(current.Policy);
            if (document.RootElement.TryGetProperty("Statement", out var existing))
                statements.AddRange(existing.EnumerateArray().Select(x =>
                    JsonSerializer.Deserialize<Dictionary<string, object?>>(x.GetRawText())!));
        }

        var sid = "MyServiceBus-" + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(topicArn)))[..16];
        if (statements.Any(x => string.Equals(x.GetValueOrDefault("Sid")?.ToString(), sid, StringComparison.Ordinal)))
            return;
        statements.Add(new Dictionary<string, object?>
        {
            ["Sid"] = sid,
            ["Effect"] = "Allow",
            ["Principal"] = new Dictionary<string, string> { ["Service"] = "sns.amazonaws.com" },
            ["Action"] = "sqs:SendMessage",
            ["Resource"] = queueArn,
            ["Condition"] = new Dictionary<string, object>
            {
                ["ArnEquals"] = new Dictionary<string, string> { ["aws:SourceArn"] = topicArn }
            }
        });
        var policy = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["Version"] = "2012-10-17",
            ["Statement"] = statements
        });
        await _sqs.SetQueueAttributesAsync(queueUrl,
            new Dictionary<string, string> { [QueueAttributeName.Policy] = policy }, cancellationToken).ConfigureAwait(false);
    }

    private Uri CreateAddress(string entityName, bool topic, string? extraQuery = null)
    {
        if (topic)
            AmazonSqsEntityName.ValidateTopic(entityName);
        else
            AmazonSqsEntityName.Validate(entityName);
        var builder = new UriBuilder(new Uri(_baseAddress, Uri.EscapeDataString(entityName)));
        var query = topic ? "type=topic" : string.Empty;
        if (!string.IsNullOrWhiteSpace(extraQuery))
            query = string.IsNullOrEmpty(query) ? extraQuery : query + "&" + extraQuery;
        builder.Query = query;
        return builder.Uri;
    }
}
