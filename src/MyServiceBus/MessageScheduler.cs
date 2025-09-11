using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public class MessageScheduler : IMessageScheduler
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly IJobScheduler _jobScheduler;

    public MessageScheduler(IPublishEndpoint publishEndpoint, ISendEndpointProvider sendEndpointProvider, IJobScheduler jobScheduler)
    {
        _publishEndpoint = publishEndpoint;
        _sendEndpointProvider = sendEndpointProvider;
        _jobScheduler = jobScheduler;
    }

    public async Task<ScheduledMessageHandle> SchedulePublish<T>(T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class
    {
        var tokenId = await _jobScheduler.Schedule(scheduledTime, ct => _publishEndpoint.Publish(message, cancellationToken: ct), cancellationToken);
        return new ScheduledMessageHandle(tokenId, scheduledTime);
    }

    public Task<ScheduledMessageHandle> SchedulePublish<T>(T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
        => SchedulePublish(message, DateTime.UtcNow + delay, cancellationToken);

    public async Task<ScheduledMessageHandle> ScheduleSend<T>(Uri destination, T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class
    {
        async Task Callback(CancellationToken ct)
        {
            var endpoint = await _sendEndpointProvider.GetSendEndpoint(destination);
            await endpoint.Send(message, cancellationToken: ct);
        }
        var tokenId = await _jobScheduler.Schedule(scheduledTime, Callback, cancellationToken);
        return new ScheduledMessageHandle(tokenId, scheduledTime);
    }

    public Task<ScheduledMessageHandle> ScheduleSend<T>(Uri destination, T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
        => ScheduleSend(destination, message, DateTime.UtcNow + delay, cancellationToken);

    public Task CancelScheduledPublish(Guid tokenId, CancellationToken cancellationToken = default)
        => _jobScheduler.Cancel(tokenId);

    public Task CancelScheduledPublish(ScheduledMessageHandle handle, CancellationToken cancellationToken = default)
        => CancelScheduledPublish(handle.TokenId, cancellationToken);

    public Task CancelScheduledSend(Guid tokenId, CancellationToken cancellationToken = default)
        => _jobScheduler.Cancel(tokenId);

    public Task CancelScheduledSend(ScheduledMessageHandle handle, CancellationToken cancellationToken = default)
        => CancelScheduledSend(handle.TokenId, cancellationToken);
}
