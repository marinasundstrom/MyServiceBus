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

    public RabbitMqTransportFactory(ConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    [Throws(typeof(OverflowException), typeof(InvalidOperationException), typeof(ArgumentException), typeof(OperationCanceledException))]
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
                queue = path;
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

    [Throws(typeof(ObjectDisposedException))]
    public async Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTopology topology,
        Func<ReceiveContext, Task> handler,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionProvider.GetOrCreateConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: topology.ExchangeName,
            type: topology.ExchangeType,
            durable: topology.Durable,
            autoDelete: topology.AutoDelete,
            cancellationToken: cancellationToken
        );

        var hasErrorQueue = !topology.AutoDelete;
        IDictionary<string, object?>? mainQueueArguments = null;

        if (hasErrorQueue)
        {
            var errorExchange = topology.QueueName + "_error";
            var errorQueue = errorExchange;

            await channel.ExchangeDeclareAsync(
                exchange: errorExchange,
                type: ExchangeType.Fanout,
                durable: topology.Durable,
                autoDelete: topology.AutoDelete,
                cancellationToken: cancellationToken
            );

            await channel.QueueDeclareAsync(
                queue: errorQueue,
                durable: topology.Durable,
                exclusive: false,
                autoDelete: topology.AutoDelete,
                cancellationToken: cancellationToken
            );

            await channel.QueueBindAsync(
                queue: errorQueue,
                exchange: errorExchange,
                routingKey: string.Empty,
                cancellationToken: cancellationToken
            );

            mainQueueArguments = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = errorExchange
            };
        }

        await channel.QueueDeclareAsync(
            queue: topology.QueueName,
            durable: topology.Durable,
            exclusive: false,
            autoDelete: topology.AutoDelete,
            arguments: mainQueueArguments,
            cancellationToken: cancellationToken
        );

        await channel.QueueBindAsync(
            queue: topology.QueueName,
            exchange: topology.ExchangeName,
            routingKey: topology.RoutingKey,
            cancellationToken: cancellationToken
        );

        return new RabbitMqReceiveTransport(channel, topology.QueueName, handler, hasErrorQueue);
    }

    [Throws(typeof(OverflowException), typeof(ArgumentException))]
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
        }
    }
}