using System.Collections.Concurrent;
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

    public async Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
    {
        string exchange;
        try
        {
            exchange = ExtractExchange(address);
        }
        catch (InvalidOperationException)
        {
            exchange = "default";
        }

        if (!_sendTransports.TryGetValue(exchange, out var sendTransport))
        {
            var connection = await _connectionProvider.GetOrCreateConnectionAsync(cancellationToken);
            var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await channel.ExchangeDeclareAsync(exchange, type: ExchangeType.Fanout, durable: true, cancellationToken: cancellationToken);
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
}