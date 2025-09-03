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

    public RabbitMqTransportFactory(ConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    [Throws(typeof(OverflowException), typeof(InvalidOperationException), typeof(ArgumentException))]
    public async Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
    {
        string exchange;
        bool durable = true;
        bool autoDelete = false;
        try
        {
            exchange = ExtractExchange(address);
            ParseExchangeSettings(address, ref durable, ref autoDelete);
        }
        catch (InvalidOperationException)
        {
            exchange = "default";
        }

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

        await channel.QueueDeclareAsync(
            queue: topology.QueueName,
            durable: topology.Durable,
            exclusive: false,
            autoDelete: topology.AutoDelete,
            cancellationToken: cancellationToken
        );

        await channel.QueueBindAsync(
            queue: topology.QueueName,
            exchange: topology.ExchangeName,
            routingKey: topology.RoutingKey,
            cancellationToken: cancellationToken
        );

        return new RabbitMqReceiveTransport(channel, topology.QueueName, handler);
    }

    [Throws(typeof(InvalidOperationException))]
    private string ExtractExchange(Uri address)
    {
        // Very simple mapping logic for now
        try
        {
            return address.Segments.LastOrDefault()?.Trim('/') ?? "default";
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Could not extract exchange from '{address}'", ex);
        }
    }

    [Throws(typeof(InvalidOperationException), typeof(OverflowException), typeof(ArgumentException))]
    private static void ParseExchangeSettings(Uri address, ref bool durable, ref bool autoDelete)
    {
        if (string.IsNullOrEmpty(address.Query))
            return;

        var query = address.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
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