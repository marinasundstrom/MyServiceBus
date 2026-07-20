using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyServiceBus.Topology;
using RabbitMQ.Client;

namespace MyServiceBus;

public sealed class RabbitMqTransportFactory : ITransportFactory
{
    private readonly ConnectionProvider _connectionProvider;
    private readonly ConcurrentDictionary<string, ISendTransport> _sendTransports = new();
    private readonly ConcurrentDictionary<string, ISendTransport> _queueTransports = new();
    private readonly ushort _prefetchCount;
    private readonly Uri _baseAddress;

    public TransportCapabilityDescriptor Capabilities => TransportCapabilityDescriptors.RabbitMq;

    public RabbitMqTransportFactory(ConnectionProvider connectionProvider, IRabbitMqFactoryConfigurator configurator)
    {
        _connectionProvider = connectionProvider;
        _prefetchCount = configurator.PrefetchCount;
        _baseAddress = new UriBuilder("rabbitmq", configurator.ClientHost, configurator.ClientPort).Uri;
    }

    public Uri GetPublishAddress(string entityName) => new(_baseAddress, $"exchange/{entityName}");

    public Uri GetTemporaryEndpointAddress(string endpointName) =>
        new(_baseAddress, $"exchange/{endpointName}?durable=false&autodelete=true");

    public Uri GetErrorAddress(string endpointName) => GetPublishAddress(endpointName + "_error");

    public Uri GetFaultAddress(string endpointName) => GetPublishAddress(endpointName + "_fault");

    public async Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
    {
        string? exchange = null;
        string? queue = null;
        bool durable = true;
        bool autoDelete = false;

        if (address.Scheme.Equals("exchange", StringComparison.OrdinalIgnoreCase))
        {
            var spec = address.OriginalString.Substring("exchange:".Length);
            string? query = null;
            var idx = spec.IndexOf('?');
            if (idx >= 0)
            {
                query = spec[(idx + 1)..];
                spec = spec[..idx];
            }

            exchange = spec;
            ParseExchangeSettings(query, ref durable, ref autoDelete);
        }
        else if (address.Scheme.Equals("queue", StringComparison.OrdinalIgnoreCase))
        {
            var spec = address.OriginalString.Substring("queue:".Length);
            string? query = null;
            var idx = spec.IndexOf('?');
            if (idx >= 0)
            {
                query = spec[(idx + 1)..];
                spec = spec[..idx];
            }

            queue = spec;
            ParseExchangeSettings(query, ref durable, ref autoDelete);
        }
        else
        {
            var path = address.AbsolutePath.Trim('/');
            if (path.StartsWith("exchange/", StringComparison.OrdinalIgnoreCase))
            {
                exchange = path["exchange/".Length..];
            }
            else if (!string.IsNullOrEmpty(path))
            {
                exchange = path;
            }

            ParseExchangeSettings(address.Query.TrimStart('?'), ref durable, ref autoDelete);

            if (exchange == null && queue == null)
                exchange = "default";
        }

        if (queue != null)
        {
            if (!_queueTransports.TryGetValue(queue, out var queueTransport))
            {
                var connection = await _connectionProvider.GetOrCreateConnectionAsync(cancellationToken);
                var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

                if (!autoDelete)
                {
                    var errorExchange = queue + "_error";
                    var errorQueue = errorExchange;
                    var skippedExchange = queue + "_skipped";
                    var skippedQueue = skippedExchange;

                    await channel.ExchangeDeclareAsync(
                        exchange: errorExchange,
                        type: ExchangeType.Fanout,
                        durable: durable,
                        autoDelete: autoDelete,
                        cancellationToken: cancellationToken);

                    await channel.QueueDeclareAsync(
                        queue: errorQueue,
                        durable: durable,
                        exclusive: false,
                        autoDelete: autoDelete,
                        cancellationToken: cancellationToken);

                    await channel.QueueBindAsync(
                        queue: errorQueue,
                        exchange: errorExchange,
                        routingKey: string.Empty,
                        cancellationToken: cancellationToken);

                    await channel.ExchangeDeclareAsync(
                        exchange: skippedExchange,
                        type: ExchangeType.Fanout,
                        durable: durable,
                        autoDelete: autoDelete,
                        cancellationToken: cancellationToken);

                    await channel.QueueDeclareAsync(
                        queue: skippedQueue,
                        durable: durable,
                        exclusive: false,
                        autoDelete: autoDelete,
                        cancellationToken: cancellationToken);

                    await channel.QueueBindAsync(
                        queue: skippedQueue,
                        exchange: skippedExchange,
                        routingKey: string.Empty,
                        cancellationToken: cancellationToken);

                    var faultExchange = queue + "_fault";
                    var faultQueue = faultExchange;

                    await channel.ExchangeDeclareAsync(
                        exchange: faultExchange,
                        type: ExchangeType.Fanout,
                        durable: durable,
                        autoDelete: autoDelete,
                        cancellationToken: cancellationToken);

                    await channel.QueueDeclareAsync(
                        queue: faultQueue,
                        durable: durable,
                        exclusive: false,
                        autoDelete: autoDelete,
                        cancellationToken: cancellationToken);

                    await channel.QueueBindAsync(
                        queue: faultQueue,
                        exchange: faultExchange,
                        routingKey: string.Empty,
                        cancellationToken: cancellationToken);
                }

                await channel.QueueDeclareAsync(
                    queue: queue,
                    durable: durable,
                    exclusive: false,
                    autoDelete: autoDelete,
                    cancellationToken: cancellationToken);

                queueTransport = new RabbitMqQueueSendTransport(channel, queue);
                _queueTransports.TryAdd(queue, queueTransport);
            }

            return queueTransport;
        }

        exchange ??= "default";
        if (!_sendTransports.TryGetValue(exchange, out var sendTransport))
        {
            var connection = await _connectionProvider.GetOrCreateConnectionAsync(cancellationToken);
            var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await channel.ExchangeDeclareAsync(exchange, type: ExchangeType.Fanout, durable: durable, autoDelete: autoDelete, cancellationToken: cancellationToken);
            sendTransport = new RabbitMqSendTransport(channel, exchange);

            _sendTransports.TryAdd(exchange, sendTransport);
        }

        return sendTransport;
    }

    public async Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTopology topology,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered = null,
        CancellationToken cancellationToken = default)
    {
        var rabbitMqTopology = RabbitMqReceiveEndpointTopology.Project(topology);
        var connection = await _connectionProvider.GetOrCreateConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var prefetch = rabbitMqTopology.PrefetchCount > 0 ? rabbitMqTopology.PrefetchCount : _prefetchCount;
        if (prefetch > 0)
            await channel.BasicQosAsync(0, prefetch, false, cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: rabbitMqTopology.ExchangeName,
            type: rabbitMqTopology.ExchangeType,
            durable: rabbitMqTopology.Durable,
            autoDelete: rabbitMqTopology.AutoDelete,
            cancellationToken: cancellationToken
        );

        var hasErrorQueue = !rabbitMqTopology.AutoDelete;

        if (hasErrorQueue)
        {
            var errorExchange = rabbitMqTopology.QueueName + "_error";
            var errorQueue = errorExchange;
            var skippedExchange = rabbitMqTopology.QueueName + "_skipped";
            var skippedQueue = skippedExchange;

            await channel.ExchangeDeclareAsync(
                exchange: errorExchange,
                type: ExchangeType.Fanout,
                durable: rabbitMqTopology.Durable,
                autoDelete: rabbitMqTopology.AutoDelete,
                cancellationToken: cancellationToken
            );

            await channel.QueueDeclareAsync(
                queue: errorQueue,
                durable: rabbitMqTopology.Durable,
                exclusive: false,
                autoDelete: rabbitMqTopology.AutoDelete,
                cancellationToken: cancellationToken
            );

            await channel.QueueBindAsync(
                queue: errorQueue,
                exchange: errorExchange,
                routingKey: string.Empty,
                cancellationToken: cancellationToken
            );

            await channel.ExchangeDeclareAsync(
                exchange: skippedExchange,
                type: ExchangeType.Fanout,
                durable: rabbitMqTopology.Durable,
                autoDelete: rabbitMqTopology.AutoDelete,
                cancellationToken: cancellationToken
            );

            await channel.QueueDeclareAsync(
                queue: skippedQueue,
                durable: rabbitMqTopology.Durable,
                exclusive: false,
                autoDelete: rabbitMqTopology.AutoDelete,
                cancellationToken: cancellationToken
            );

            await channel.QueueBindAsync(
                queue: skippedQueue,
                exchange: skippedExchange,
                routingKey: string.Empty,
                cancellationToken: cancellationToken
            );

            var faultExchange = rabbitMqTopology.QueueName + "_fault";
            var faultQueue = faultExchange;

            await channel.ExchangeDeclareAsync(
                exchange: faultExchange,
                type: ExchangeType.Fanout,
                durable: rabbitMqTopology.Durable,
                autoDelete: rabbitMqTopology.AutoDelete,
                cancellationToken: cancellationToken
            );

            await channel.QueueDeclareAsync(
                queue: faultQueue,
                durable: rabbitMqTopology.Durable,
                exclusive: false,
                autoDelete: rabbitMqTopology.AutoDelete,
                cancellationToken: cancellationToken
            );

            await channel.QueueBindAsync(
                queue: faultQueue,
                exchange: faultExchange,
                routingKey: string.Empty,
                cancellationToken: cancellationToken
            );
        }

        await channel.QueueDeclareAsync(
            queue: rabbitMqTopology.QueueName,
            durable: rabbitMqTopology.Durable,
            exclusive: false,
            autoDelete: rabbitMqTopology.AutoDelete,
            arguments: rabbitMqTopology.QueueArguments,
            cancellationToken: cancellationToken
        );

        await channel.QueueBindAsync(
            queue: rabbitMqTopology.QueueName,
            exchange: rabbitMqTopology.ExchangeName,
            routingKey: rabbitMqTopology.RoutingKey,
            cancellationToken: cancellationToken
        );

        var errorAddress = hasErrorQueue ? GetErrorAddress(rabbitMqTopology.QueueName) : null;
        var faultAddress = hasErrorQueue ? GetFaultAddress(rabbitMqTopology.QueueName) : null;
        return new RabbitMqReceiveTransport(channel, rabbitMqTopology.QueueName, handler, errorAddress, faultAddress, isMessageTypeRegistered);
    }

    private static void ParseExchangeSettings(string? queryString, ref bool durable, ref bool autoDelete)
    {
        if (string.IsNullOrEmpty(queryString))
            return;

        var query = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query)
        {
            var kv = part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (kv.Length != 2)
                continue;

            var key = kv[0];
            var value = kv[1];

            if (key.Equals("durable", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out var d))
                durable = d;
            else if (key.Equals("autodelete", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out var ad))
                autoDelete = ad;
            else if (key.Equals("temporary", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out var temporary) && temporary)
            {
                durable = false;
                autoDelete = true;
            }
        }
    }
}
