using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using MyServiceBus.RabbitMq;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public class RabbitMqEndpoint : IEndpoint
{
    private readonly ISendEndpoint _sendEndpoint;
    private readonly ConnectionProvider _connectionProvider;
    private readonly string _queueName;

    public RabbitMqEndpoint(ISendEndpoint sendEndpoint, ConnectionProvider connectionProvider, string queueName)
    {
        _sendEndpoint = sendEndpoint;
        _connectionProvider = connectionProvider;
        _queueName = queueName;
    }

    public EndpointCapabilities Capabilities =>
        EndpointCapabilities.Acknowledgement | EndpointCapabilities.Retry | EndpointCapabilities.BatchSend;

    public Task Send<T>(T message, Action<ISendContext>? configure = null, CancellationToken cancellationToken = default)
        => _sendEndpoint.Send(message, configure, cancellationToken);

    public async IAsyncEnumerable<ReceiveContext> ReadAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var connection = await _connectionProvider.GetOrCreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var contextFactory = new MessageContextFactory();

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await channel.BasicGetAsync(_queueName, false, cancellationToken);
            if (result is null)
            {
                await Task.Delay(200, cancellationToken);
                continue;
            }

            try
            {
                var props = result.BasicProperties;
                var headers = props.Headers?.ToDictionary(x => x.Key, x => (object)(x.Value ?? string.Empty))
                              ?? new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(props.ContentType))
                    headers["content_type"] = props.ContentType!;
                else if (!headers.ContainsKey("content_type"))
                    headers["content_type"] = "application/vnd.masstransit+json";

                var transport = new RabbitMqTransportMessage(headers, props.Persistent, result.Body.ToArray());
                var messageContext = contextFactory.CreateMessageContext(transport);
                yield return new ReceiveContextImpl(messageContext, null, cancellationToken);
            }
            finally
            {
                await channel.BasicAckAsync(result.DeliveryTag, false, cancellationToken);
            }
        }
    }

    public IDisposable Subscribe(Func<ReceiveContext, Task> handler)
    {
        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            await foreach (var ctx in ReadAsync(cts.Token))
                await handler(ctx);
        }, cts.Token);
        return cts;
    }
}

