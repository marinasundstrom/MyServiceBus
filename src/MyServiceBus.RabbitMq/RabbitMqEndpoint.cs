using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

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

    public async IAsyncEnumerable<ConsumeContext> ReadAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var connection = await _connectionProvider.GetOrCreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

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
                var envelope = JsonSerializer.Deserialize<Envelope<object>>(result.Body.ToArray(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (envelope != null)
                    yield return new DefaultConsumeContext<Envelope<object>>(envelope);
            }
            finally
            {
                await channel.BasicAckAsync(result.DeliveryTag, false, cancellationToken);
            }
        }
    }

    public IDisposable Subscribe(Func<ConsumeContext, Task> handler)
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

