using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace MyServiceBus.Monitoring.Server;

public sealed class MonitoringChangeFeed
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> subscribers = new();

    public void Publish(string changeType)
    {
        var message = JsonSerializer.Serialize(new
        {
            type = changeType,
            occurredAtUtc = DateTimeOffset.UtcNow
        });
        foreach (var subscriber in subscribers.Values)
            subscriber.Writer.TryWrite(message);
    }

    public async Task Stream(HttpContext context, CancellationToken cancellationToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        subscribers[id] = channel;
        try
        {
            await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(bytes, System.Net.WebSockets.WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            subscribers.TryRemove(id, out _);
        }
    }
}
