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
        => _publishEndpoint.Publish(message, ctx => ctx.SetScheduledEnqueueTime(scheduledTime), cancellationToken);

    public Task SchedulePublish<T>(T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
        => SchedulePublish(message, DateTime.UtcNow + delay, cancellationToken);

    public async Task ScheduleSend<T>(Uri destination, T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class
    {
        var endpoint = await _sendEndpointProvider.GetSendEndpoint(destination);
        await endpoint.Send(message, ctx => ctx.SetScheduledEnqueueTime(scheduledTime), cancellationToken);
    }

    public Task ScheduleSend<T>(Uri destination, T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
        => ScheduleSend(destination, message, DateTime.UtcNow + delay, cancellationToken);
}
