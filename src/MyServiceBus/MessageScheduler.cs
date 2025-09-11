using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public class MessageScheduler : IMessageScheduler
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ISendEndpointProvider _sendEndpointProvider;

    public MessageScheduler(IPublishEndpoint publishEndpoint, ISendEndpointProvider sendEndpointProvider)
    {
        _publishEndpoint = publishEndpoint;
        _sendEndpointProvider = sendEndpointProvider;
    }

    public Task SchedulePublish<T>(T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class
        => Schedule(async ct => await _publishEndpoint.Publish(message, cancellationToken: ct), scheduledTime, cancellationToken);

    public Task SchedulePublish<T>(T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
        => SchedulePublish(message, DateTime.UtcNow + delay, cancellationToken);

    public Task ScheduleSend<T>(Uri destination, T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class
        => Schedule(async ct =>
        {
            var endpoint = await _sendEndpointProvider.GetSendEndpoint(destination);
            await endpoint.Send(message, cancellationToken: ct);
        }, scheduledTime, cancellationToken);

    public Task ScheduleSend<T>(Uri destination, T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
        => ScheduleSend(destination, message, DateTime.UtcNow + delay, cancellationToken);

    static async Task Schedule(Func<CancellationToken, Task> action, DateTime scheduledTime, CancellationToken cancellationToken)
    {
        var delay = scheduledTime - DateTime.UtcNow;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken);

        await action(cancellationToken);
    }
}
